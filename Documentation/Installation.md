# Installation and development

## Prerequisites checked in the current environment

- Windows 11, .NET SDK `10.0.401`, ASP.NET Core runtime `10.0.12`.
- Visual Studio Community 2026 `18.10.2` (the requested VS 2022 is not installed).
- Android, iOS, Mac Catalyst, and Windows MAUI workloads are installed. This project targets Android, iOS, and Windows.
- SQL Server Express (`SQLEXPRESS`) is installed and running; SSMS 22 is installed.
- iOS builds require a paired Mac with Xcode. This Windows machine cannot build or sign an iOS application by itself.

The project targets .NET 10 to match the installed SDK and MAUI workload. Visual Studio 2022 is not installed, so use Visual Studio 2026 or install a compatible Visual Studio release with the .NET MAUI workload.

## Restore and build

From `D:\my project\EDOC`:

```powershell
dotnet restore EDOC.slnx
dotnet build EDOC.Api\EDOC.Api.csproj
dotnet test EDOC.Api.Tests\EDOC.Api.Tests.csproj
dotnet build EDOC.Mobile\EDOC.Mobile.csproj -f net10.0-windows10.0.19041.0
dotnet build EDOC.Mobile\EDOC.Mobile.csproj -f net10.0-android
dotnet build EDOC.Web\EDOC.Web.csproj
```

For iOS, pair Visual Studio with a Mac that has Xcode and the matching .NET iOS workload. Then build `net10.0-ios` from that Mac-connected Visual Studio environment.

## Local secrets and API start

The API intentionally refuses to start without a JWT signing key. Create a local user secret (not a source file):

```powershell
dotnet user-secrets set "Jwt:Key" "<generate-a-random-secret-of-at-least-32-bytes>" --project EDOC.Api\EDOC.Api.csproj
```

Never commit a real signing key. The local SQL Server connection uses Windows integrated authentication in `EDOC.Api/appsettings.json`; change it through an environment-specific configuration source when deploying.

After applying the database migration, start the local development API with the HTTPS launch profile. It serves HTTPS on `https://localhost:5155` for the installed Windows app and HTTP on `http://localhost:5154` for development clients:

```powershell
dotnet run --project EDOC.Api\EDOC.Api.csproj --launch-profile https
```

Set the app API address using the `Preferences` key `api_base_url`. Android emulator Debug defaults to `http://10.0.2.2:5154/`; Windows and iOS simulator Debug default to `http://localhost:5154/`. Installed Release builds default to `https://localhost:5155/`. Configure the deployed HTTPS URL for production. The development profile binds to localhost and must not be exposed to public networks.

## Role-specific clients

- `EDOC.Mobile` is one Android/iOS app with role-aware Patient, Doctor, and Caregiver workspaces. One mobile package serves all three roles after sign-in; the account determines the screens. Administrator and HospitalStaff accounts are directed to the web portal.
- `EDOC.Web` is the browser portal for Administrator and HospitalStaff. The server signs the API session into a protected, HttpOnly cookie, keeps API access tokens server-side, refreshes API tokens, and enforces role-specific page authorization. The browser never receives the API bearer token.
- The administrator portal provides platform metrics, doctor credential approval, account activation, hospital staff assignment, and the facility directory. Hospital staff can view and update appointments scoped by the API to their assigned facilities.

In local development, start the API and portal in separate terminals. The portal development configuration points to the API HTTP profile at port 5154:

```powershell
$env:Jwt__Key = "<local random signing key of at least 32 bytes>"
dotnet run --project EDOC.Api\EDOC.Api.csproj --launch-profile http
```

```powershell
dotnet run --project EDOC.Web\EDOC.Web.csproj --launch-profile http
```

Open `http://localhost:5170`. Override the portal API address with `Api__BaseUrl` if the API is hosted elsewhere. Before deployment, configure a reachable HTTPS API address, HTTPS termination, persistent ASP.NET Core Data Protection keys, and an appropriate SQL connection string through deployment secrets/environment configuration. The portal currently uses an in-memory server-side ticket store; restarting its process signs active web sessions out, and multiple web instances need a shared distributed ticket store.

For Android, build one installable app package for the three mobile roles:

```powershell
dotnet publish EDOC.Mobile\EDOC.Mobile.csproj -f net10.0-android -c Release
```

The Android package needs an HTTPS API URL reachable from the device and a production signing keystore before distribution. This checkout does not include a production endpoint or signing credentials, so a production APK/AAB is not ready for distribution.

To publish a Windows web host executable, configure the production API URL and HTTPS hosting before running:

```powershell
dotnet publish EDOC.Web\EDOC.Web.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts\EDOC.Web
```

This creates one web-server executable (`EDOC.Web.exe`), not separate executable files per role. Keep the adjacent `wwwroot` and `appsettings.json` files with the executable. Users open the hosted portal in a browser; the account role selects the Administrator or HospitalStaff workspace.

## Git

This folder is initialized as a Git repository. The `.gitignore` excludes build output, local secrets, and private document files.
