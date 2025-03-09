/*
 * DICOMService.cs
 * 
 * Overview:
 * This file defines the DICOMService class, which manages DICOM networking operations within a .NET MAUI application.
 * It provides implementations for DICOM C-STORE and C-ECHO services.
 * The service also supports receiving and storing DICOM images locally on Android devices.
 * 
 * Features:
 * - Implements DICOM storage SCP for receiving and storing DICOM images
 * - Supports C-STORE,C-ECHO operations
 * - Handles association requests and releases
 * - Supports various transfer syntaxes, including compressed formats
 * 
 * Properties:
 * - StorageServer: Manages the DICOM storage service instance
 * - Handles DICOM requests and responses for networking operations
 * 
 * When testing Storage-SCP on the Android emulator, port forwarding is required. The command is as follows:
 *   adb forward tcp:[port number] tcp:[port number]
 * The IP address for the Storage SCU is 127.0.0.1 or localhost.
 * 
 * Author: s.harada@HIBMS
 */
using FellowOakDicom.Network.Client;
using FellowOakDicom.Network;
using FellowOakDicom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.PlatformConfiguration;

namespace SATOMI.Pages
{
    class DICOMService
    {
        public static IDicomServer? StorageServer;
        public class DicomStorageServer : DicomService, IDicomServiceProvider, IDicomCStoreProvider, IDicomCEchoProvider
        {
            private string _storagePath = "";
            private string _ae_title = "STORESCP";
            private int _receivedFiles = 0;
            private string remoteIP = "";
            private string callingAE = "";
            private string _recievedModality = "UNKNOWN";
            private static readonly DicomTransferSyntax[] _acceptedTransferSyntaxes = new DicomTransferSyntax[]
            {
               DicomTransferSyntax.ExplicitVRLittleEndian,
               DicomTransferSyntax.ExplicitVRBigEndian,
               DicomTransferSyntax.ImplicitVRLittleEndian
            };

            private static readonly DicomTransferSyntax[] _acceptedImageTransferSyntaxes = new DicomTransferSyntax[]
            {
               DicomTransferSyntax.JPEGLSLossless,
               DicomTransferSyntax.JPEG2000Lossless,
               DicomTransferSyntax.JPEGProcess14SV1,
               DicomTransferSyntax.JPEGProcess14,
               DicomTransferSyntax.RLELossless,
               DicomTransferSyntax.JPEGLSNearLossless,
               DicomTransferSyntax.JPEG2000Lossy,
               DicomTransferSyntax.JPEGProcess1,
               DicomTransferSyntax.JPEGProcess2_4,
               DicomTransferSyntax.ExplicitVRLittleEndian,
               DicomTransferSyntax.ExplicitVRBigEndian,
               DicomTransferSyntax.ImplicitVRLittleEndian
            };
            public DicomStorageServer(INetworkStream stream, Encoding fallbackEncoding, ILogger log, DicomServiceDependencies dependencies)
                : base(stream, fallbackEncoding, log, dependencies)
            {
                _ae_title = Preferences.Get("SCPAeTitle", "STORESCP");
#if ANDROID
                _storagePath = FileSystem.AppDataDirectory;
                string dicomStoragePath = Path.Combine(_storagePath, "DICOMStorage");
                if (Directory.Exists(dicomStoragePath))
                {
                    foreach (string file in Directory.GetFiles(dicomStoragePath))
                    {
                        File.Delete(file);
                    }
                }
                else
                {
                    Directory.CreateDirectory(dicomStoragePath);
                }
                _storagePath = dicomStoragePath;
#endif
            }
            public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
            {
                _receivedFiles = 0;
                remoteIP = association.RemoteHost;
                callingAE = association.CallingAE;

                if (association.CalledAE != _ae_title)
                {
                    return SendAssociationRejectAsync(
                        DicomRejectResult.Permanent,
                        DicomRejectSource.ServiceUser,
                        DicomRejectReason.CalledAENotRecognized);
                }
                foreach (var pc in association.PresentationContexts)
                {
                    if (pc.AbstractSyntax == DicomUID.Verification)
                    {
                        pc.AcceptTransferSyntaxes(_acceptedTransferSyntaxes);
                    }
                    else if (pc.AbstractSyntax.StorageCategory != DicomStorageCategory.None)
                    {
                        pc.AcceptTransferSyntaxes(_acceptedImageTransferSyntaxes);
                    }
                }

                return SendAssociationAcceptAsync(association);
            }

            public Task OnReceiveAssociationReleaseRequestAsync()
            {
                MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var window = Application.Current?.Windows.FirstOrDefault();
                    if (window?.Page != null)
                    {
                        var currentPage = Shell.Current?.CurrentPage;
                        if (currentPage is PatientListPage patientListPage)
                        {
                            await patientListPage.LoadDicomFilesAsync();
                        }
                    }
                });
                PatientListPage.updated_data = true;
                return SendAssociationReleaseResponseAsync();
            }

            public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
            {
#if ANDROID
                if (Directory.Exists(_storagePath))
                {
                    foreach (string file in Directory.GetFiles(_storagePath))
                    {
                        File.Delete(file);
                    }
                }
#endif
            }

            public void OnConnectionClosed(Exception exception)
            {
                /* nothing to do here */
            }

            public async Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
            {
                var studyUid = request.Dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID).Trim();
                var instUid = request.SOPInstanceUID.UID;
                _recievedModality = request.Dataset.GetSingleValueOrDefault(DicomTag.Modality, "UNKNOWN");

                var path = Path.GetFullPath(_storagePath);
                path = Path.Combine(path, studyUid);

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                path = Path.Combine(path, instUid) + ".dcm";

                await request.File.SaveAsync(path);

                Interlocked.Increment(ref _receivedFiles);

                return new DicomCStoreResponse(request, DicomStatus.Success);
            }

            public Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
            {
                return Task.CompletedTask;
            }


            public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request)
            {
                return Task.FromResult(new DicomCEchoResponse(request, DicomStatus.Success));
            }
        }

        public static async Task SendDicomFile(string serverIP, int port, bool useTLS , string scu_AE, string scp_AE ,string filePath)
        {
            var client = DicomClientFactory.Create(serverIP, port, useTLS, scu_AE, scp_AE);
            var file = DicomFile.Open(filePath);
            var storeRequest = new DicomCStoreRequest(file);
            await client.AddRequestAsync(storeRequest);
            await client.SendAsync(); 
            Console.WriteLine("DICOM file sent.");
        }

        public static async Task QueryDicomServer(string serverIP, int port, bool useTLS, string scu_AE, string scp_AE,
            string? patientID =null, 
            string? patientName = null, 
            DicomDateRange? studytime = null , 
            string? accession = null, 
            string? studyId = null,
            string? modalitiesInStudy = null,
            string? studyInstanceUid = null
            )
        {
            var cfind = DicomCFindRequest.CreateStudyQuery(patientId: patientID,
                patientName: patientName,
                studyDateTime: studytime,
                accession: accession,
                studyId: studyId,
                modalitiesInStudy: modalitiesInStudy,
                studyInstanceUid: studyInstanceUid);
            cfind.OnResponseReceived = (DicomCFindRequest rq, DicomCFindResponse rp) => {
                Console.WriteLine("PatientID: {0}", rp.Dataset.GetString(DicomTag.PatientID));
                Console.WriteLine("PatientName: {0}", rp.Dataset.GetString(DicomTag.PatientName));
                Console.WriteLine("StudyTime: {0}", rp.Dataset.GetString(DicomTag.StudyTime));
                Console.WriteLine("AccessionNumber: {0}", rp.Dataset.GetString(DicomTag.AccessionNumber));
                Console.WriteLine("ModalitiesInStudy: {0}", rp.Dataset.GetString(DicomTag.ModalitiesInStudy));
                Console.WriteLine("Study UID: {0}", rp.Dataset.GetString(DicomTag.StudyInstanceUID));
            };
            var client = DicomClientFactory.Create(serverIP, port, useTLS, scu_AE, scp_AE);
            await client.AddRequestAsync(cfind);
            await client.SendAsync();
        }

        public static async Task RetrieveDicomFile(string serverIP, int port, string destinationAE,
            string? patientID = null,
            string? patientName = null,
            DicomDateRange? studytime = null,
            string? accession = null,
            string? studyId = null,
            string? modalitiesInStudy = null,
            string? studyInstanceUid = null)
        {
            var cmove = new DicomCMoveRequest(destinationAE, studyInstanceUid: studyInstanceUid);

            var client = DicomClientFactory.Create("127.0.0.1", 11112, false, "SCU-AE", "SCP-AE");
            await client.AddRequestAsync(cmove);
            await client.SendAsync();
        }
    }
}
