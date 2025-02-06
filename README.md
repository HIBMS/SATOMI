# SATOMI（DICOM Viewer for Smartphone）

## Overview
**This software is for research and educational purposes only. It is not intended for clinical use.**
SATOMI is a lightweight and intuitive DICOM image viewer designed for mobile devices. It allows user to view and analyze medical images on their smartphones with ease.

## Features
- **DICOM File Support**: Load and display DICOM images from local storage.(Cloud services support is not yet available.)
- **Windowing & Adjustments**: Adjust brightness, contrast, and apply preset windowing modes.
- **Zoom & Pan**: Intuitive touch gestures for zooming and panning.
- **Cross-Platform Compatibility**: Available for both iOS and Android.
- **Modality Support**: Currently supports CT (Computed Tomography) images.
  
## Installation
### iOS (TestFlight)
*Installation instructions are currently being adjusted.*
### Android (Google Play Beta)
*Installation instructions are currently being adjusted.*

## Usage
1. Open the app and grant necessary permissions.
2. Load DICOM files from local storage.(Cloud services support is not yet available.)
3. Use touch gestures to navigate and analyze images.

## Development
### Prerequisites
- Visual Studio 2022
- C#
- fo-dicom (DICOM library)
- epj.ProgressBar.Maui

### Setup
```sh
# Clone the repository
1. Clone the repository:
   git clone https://github.com/HIBMS/SATOMI.git
   cd SATOMI
2. Open the project in Visual Studio.
3. Restore NuGet packages.
4. Build and run the project for Android (iOS testing is not yet conducted).
```

## License
This project is licensed under the GNU General Public License v3.0 (GPL-3.0).
See the [LICENSE](./LICENSE) file for more details.

## Author
- Hyogo Ion Beam Medical Support(https://hibms-hyogo.co.jp/)
