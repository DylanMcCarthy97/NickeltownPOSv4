# Install once + updates via GitHub

Use when the bar PC and dev PC are in different places. The bar needs internet.

## Part A - Put the project on GitHub (once)

1. Create a repo on github.com (example name: NickeltownPOSV4).
2. Push your code including the deploy folder.
3. Note YOUR_USER and YOUR_REPO names.

## Part B - Build the installer .msix (once, then every release)

Visual Studio:
1. Release + x64
2. Right-click NickeltownPOSV4 -> Publish -> Create App Packages
3. Sideloading, x64, use the SAME signing certificate every time
4. Finish; find the .msix under NickeltownPOSV4\AppPackages\

Or PowerShell from repo root:
  .\scripts\publish-update.ps1 -Version 1.0.0.0 -OutputDir .\deploy\out

## Part C - First install on the bar PC (once)

1. Copy the .msix to the bar (USB or download from GitHub).
2. Double-click the .msix on the bar -> Install.
3. Open Nickeltown POS.
4. Admin -> Software updates.
5. Update feed (paste once):
   https://raw.githubusercontent.com/YOUR_USER/YOUR_REPO/main/deploy
6. Check on startup ON. Auto install ON for kiosk.
7. Save.

## Part D - First GitHub release + manifest (once)

1. GitHub repo -> Releases -> Create new release.
2. Tag: v1.0.0.0 (match package version).
3. Upload the .msix file. Publish.
4. Edit deploy/update-manifest.json (see update-manifest.example.json):
   - version: 1.0.0.0
   - packageUri: full https URL to the .msix on that release
5. Commit and push to main.

The bar reads update-manifest.json from GitHub. The .msix file is downloaded from packageUri.

## Part E - Every new version

1. Bump version in Package.appxmanifest.
2. Build new .msix (same certificate).
3. New GitHub Release, tag v1.0.1.0, upload .msix.
4. Update deploy/update-manifest.json (version + packageUri URL).
5. git push.
6. Bar restarts app -> auto update.

## Rules

- Public GitHub repo.
- Same MSIX signing certificate every build.
- Do not delete AppData\Local\NickeltownPOSV4 on the bar (database).
