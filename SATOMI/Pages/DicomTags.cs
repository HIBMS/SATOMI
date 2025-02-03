//using Dicom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using System.Globalization;
//using Windows.ApplicationModel.Payments;

namespace SATOMI.Pages
{
    /// <summary>
    /// Class that combined
    /// </summary>
    public class DicomTags
    {
        public string PatientDetails { get; private set; } = string.Empty;
        public string StudyDetails { get; private set; } = string.Empty;
        public string ImageDetails { get; private set; } = string.Empty;
        public double WW { get; private set; } = 400.0;
        public double WL { get; private set; } = 40.0;

        public DicomTags(FellowOakDicom.DicomDataset ds)
        {
            string ptnName = ds.GetSingleValueOrDefault(FellowOakDicom.DicomTag.PatientName, string.Empty);
            string ptnID = ds.GetSingleValueOrDefault(FellowOakDicom.DicomTag.PatientID, string.Empty);
            string ptnSex = ds.GetSingleValueOrDefault(FellowOakDicom.DicomTag.PatientSex, string.Empty);
            string ptnBirthday = ds.GetSingleValueOrDefault(FellowOakDicom.DicomTag.PatientBirthDate, string.Empty);
            string studyDescription = ds.GetSingleValueOrDefault(FellowOakDicom.DicomTag.StudyDescription, string.Empty);
            string seriesDescription = ds.GetSingleValueOrDefault(FellowOakDicom.DicomTag.SeriesDescription, string.Empty);
            string institution = ds.GetSingleValueOrDefault(FellowOakDicom.DicomTag.InstitutionName, string.Empty);
            string studyDate = ds.GetSingleValueOrDefault(FellowOakDicom.DicomTag.StudyDate, string.Empty);
            string studyTime = ds.GetSingleValueOrDefault(FellowOakDicom.DicomTag.StudyTime, string.Empty);
            double windowwidth = ds.GetSingleValueOrDefault(FellowOakDicom.DicomTag.WindowWidth, 400.0);
            double windowlebel = ds.GetSingleValueOrDefault(FellowOakDicom.DicomTag.WindowCenter, 40.0);
            int frames = ds.GetSingleValueOrDefault(FellowOakDicom.DicomTag.NumberOfFrames, 0);
            int width = ds.GetSingleValueOrDefault(FellowOakDicom.DicomTag.Columns, 0);
            int height = ds.GetSingleValueOrDefault(FellowOakDicom.DicomTag.Rows, 0);

            _initialize(ptnName, ptnID, ptnSex, ptnBirthday,
                studyDescription, seriesDescription, institution, studyDate, studyTime,
                frames, width, height, windowwidth, windowlebel);
        }

        private string _dicomDateToDate(string date)
        {
            // The standard DICOM DateTime format(YYYYMMDD)
            return DateTime.ParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture).ToString("dd-MMM-yyyy");
        }

        private string _dicomTimeToTime(string time)
        {
            // The standard DICOM Time format(hhmmss)
            return DateTime.ParseExact(time, "HHmmss", CultureInfo.InvariantCulture).ToString("ss:mm:HH");
        }

        private void _initialize(string name, string id, string sex, string bday,
            string studyDescription, string seriesDescription, string institution, string studyDate, string studyTime,
            int frames, int width, int height, double windowwidth , double windowlebel)
        {
            StringBuilder sb = new StringBuilder();

            // Initialize PatientDetails
            if (!string.IsNullOrEmpty(name))
                sb.AppendLine($"Name: {name}");
            if (id.Length > 1)
                sb.AppendLine($"ID: {id} {(!string.IsNullOrEmpty(sex) ? sex : string.Empty)}");
            if (bday.Length > 1 && sb.Length > 1) // if we have some identifying information
            {
                if (bday.Length == 8 && bday.All(char.IsDigit)) // if it is in the correct YYYYMMDD format
                    bday = _dicomDateToDate(bday);

                sb.AppendLine($"DOB: {bday}");
            }

            PatientDetails = sb.ToString();
            sb.Clear();

            // Initialize StudyDetails
            if (!string.IsNullOrEmpty(studyDate))
            {
                if (studyDate.Length == 8 && studyDate.All(char.IsDigit)) // if it is in the correct YYYYMMDD format
                    studyDate = _dicomDateToDate(studyDate);

                int idx = studyTime.IndexOf(".");
                if (idx > 0)
                    studyTime = studyTime.Substring(0, idx); // sometimes the MS is included as a .MS suffix

                if (studyTime.Length == 6 && studyTime.All(char.IsDigit)) // if it is in the correct YYYYMMDD format
                    studyTime = _dicomTimeToTime(studyTime);

                sb.AppendLine($"Date: {studyDate} {(!string.IsNullOrEmpty(studyTime) ? studyTime : string.Empty)}");
            }

            if (!string.IsNullOrEmpty(institution))
            {
                institution = Regex.Replace(institution, @"\s+", " "); // sometimes there are multiple spaces
                sb.AppendLine($"Location: {institution}");
            }

            if (!string.IsNullOrEmpty(studyDescription))
                sb.AppendLine(studyDescription);
            if (!string.IsNullOrEmpty(seriesDescription))
                sb.AppendLine(seriesDescription);

            StudyDetails = sb.ToString();
            sb.Clear();

            
            WW = windowwidth;
            WL = windowlebel;

            // Initialize ImageDetails
            if (frames != 0)
                sb.AppendLine($"Frames: {frames}");
            if (width > 0 && height > 0)
                sb.Append($"Size: {width}x{height}");

            ImageDetails = sb.ToString();
            sb.Clear();
        }

        public override string ToString()
        {
            return $"{PatientDetails}" +
                   $"{(PatientDetails.Length > 1 ? Environment.NewLine : string.Empty)}" +
                   $"{StudyDetails}";
        }
    }
}
