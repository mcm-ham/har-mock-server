# HAR Mock Server

HAR mock server provides the ability to mock API requests using HAR files. This can be useful for testing or reproduction of reported bugs where a HAR file has been captured in the browser.

<https://developers.google.com/web/tools/chrome-devtools/network/reference#save-as-har>

## Installation

```sh
dotnet tool install -g mcmham.harmockserver
```

## Usage

Update your client app to use HarMockServer as the server app by updating URL to be `http://localhost:5000` (you can change this by specifying `--urls http://localhost:8000`). Then run the following command in the directory where the HAR files live (or by specifying folder via `--har-folder path/to/files`) specifying the real server app url.

```sh
harmockserver --api-url https://api.server.com
```
