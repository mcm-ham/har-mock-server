# HAR Mock Server

HAR mock server provides the ability to mock API requests using HAR files. This can be useful for testing or reproduction of reported bugs where a HAR file has been captured in the browser.

<https://developers.google.com/web/tools/chrome-devtools/network/reference#save-as-har>

Steps

* Install [.NET 7.0 SDK](https://dotnet.microsoft.com/download/dotnet/7.0)
* Update `ApiUrl` with the real API endpoint in appsettings.json.
* Update you app to use `https://localhost:8881` as the API endpoint.
* Run `dotnet run` from command-line.
* Can copy or remove any HAR file from HARS subfolder and mock server will process file without need for restarting.
