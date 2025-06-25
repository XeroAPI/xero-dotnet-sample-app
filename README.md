# Xero DotNet Sample App
This is a companion app built with net9.0 and MVC to demonstrate Xero OAuth 2.0 Client Authentication & OAuth 2.0 APIs

__IMPORTANT!__ This application is for demo only. We recommend setting up a secure token storage for your production app.

Its functions include:

- connect & reconnect to Xero
- storing Xero token in a .json file
- refresh Xero access token on expiry
- allow user to switch between tenants/organisations
- allow user to disconnect a tenant or revoke token
- allow manual testing of many Xero API endpoints
- display API call responses
- display code snippets responsible for the call
- receive Xero webhook and display the payload
- go through the Sign up with Xero (Recommended Flow) and user registration to local SQLite3 database
- Xero App Store Subscription (XASS) flow

You can connect this companion app to an actual Xero organisation and make real API calls.<br>
However, we recommend you to start with your Demo Company organisation for your testing, especially for XASS testing. [Here](https://central.xero.com/s/article/Use-the-demo-company) is how to turn it on.
<br>

### Create a Xero App
You will need your Xero app credentials created to run this demo app.

To obtain your API keys, follow these steps:

* Create a [free Xero user account](https://www.xero.com/us/signup/api/) (if you don't have one)
* Login to [Xero developer center](https://developer.xero.com/myapps)
* Click "New App" link
* Enter your App name, company url, privacy policy url.
* Enter the redirect URI (your callback url - i.e. `https://localhost:5001/Authorization/Callback`)
* Agree to terms and condition and click "Create App".
* Click "Generate a secret" button.
* Copy your client id and client secret and save for use later.
* Click the "Save" button. You secret is now hidden.
<br>
<br>

## Getting the Environment Setup Ready
Follow this instruction to get your development environment ready.

### 1. Download Visual Studio Code
[Download](https://code.visualstudio.com/download) and install dotnet SDK on your machine.<br>
Open the project root folder with VS Code.

![image](https://user-images.githubusercontent.com/41350731/76296821-ec176380-630a-11ea-8a61-5b6ba1336862.png)

Go to Extensions, install C# extension by OmniSharp. 

![image](https://user-images.githubusercontent.com/41350731/76296935-19fca800-630b-11ea-8684-916c78254618.png)

### 2. Install Dotnet SDK
First, check dotnet is installed in terminal use <code style="color:red">dotnet —info or dotnet —version</code><br>
The command should return some thing like this (at the time of writing the version number is 9.0.301):
```
$ dotnet --version
9.0.301
```
If nothing comes up, and you’re using Visual Studio Code, you’ll need to download and install from https://dotnet.microsoft.com/en-us/download/dotnet/sdk-for-vs-code<br>

If you are still not seeing it in your terminal, you may also need to add it into your ‘PATH’ in your config file.<br>
Config file depends if you’re using Bash or Zsh, and if you’re installing using Homebrew or not.

### 3. Install and get running with ngrok for webhooks
Use <code style="color:red">brew install ngrok</code><br>
Follow the steps from their [website](https://ngrok.com/).<br>
Eventually, you will need a token for using ngrok. As part of signing up for ngrok account, you will be given one which you can add it via terminal <code style="color:red">ngrok config add-authtoken \<token></code>

### 4. Install Microsoft Entity Framework Core
Once you’ve run dotnet tool, install the Microsoft Entity Framework (EF) Core with command <code style="color:red">dotnet tool install --global dotnet-ef</code>.<br>
To update (just in case) use <code style="color:red">dotnet tool update --global dotnet-ef</code> command.<br><br>
You will need to check it’s in your PATH.<br>
You can check whether you have it installed by running <code style="color:red">dotnet ef</code><br>
Add to your config file similar to above, for example by running <code style="color:red">nano ~/.bash_profile</code> and adding in <code style="color:red">export PATH="$PATH:$HOME/.dotnet/tools"</code>

### 5. Download the code
Clone this repo to your local drive or open with GitHub desktop client.

__Configure your API Keys__<br>
In /XeroDotnetSampleApp/appsettings.json, you should populate your configuration values as such: 

```
"XeroConfiguration": {
    "Scope": "offline_access openid profile email...",
    "State": "my_state",
    
    "ClientId": "YOUR_XERO_APP_CLIENT_ID",
    "ClientSecret": "YOUR_XERO_APP_CLIENT_SECRET",
    "CallbackUri": "https://localhost:5001/Authorization/Callback"
  },
  "SignUpWithXeroSettings": {
    "SignUpWithXeroScope": "openid profile email accounting.settings",
    "CallbackUri": "https://localhost:5001/SignUpWithXero/Callback"
  },
  "XeroAppStoreSubscriptionSettings": {
    "AppId": "YOUR_XERO_APP_APP_ID"
  },
  "WebhookSettings": {
    "XeroSignature": "x-xero-signature",
    "WebhookKey": "YOUR_XERO_APP_WEBHOOK_KEY"
  },
  "DatabaseConfiguration": {
    "DatabaseConnectionString": "Data Source=SignUpWithXeroUsers.db"
  }
```

For the "Scope" section, you will notice that there are minimal scopes and also excludes offline_access scope. This is to make sure the user does not falsely become a referral if they do not convert into a paying customer.<br>
More on the [Sign up with Xero](https://developer.xero.com/documentation/xero-app-store/app-partner-guides/sign-up) and [referral revenue share](https://developer.xero.com/documentation/xero-app-store/app-partner-guides/xero-app-store-referrals-and-billing) can be found on our developer portal.
<br>
<br>
If you are testing the subscription flow and other webhooks, make sure you set these two fields for your app from Xero Developer Portal.
- "Delivery URL" as <span style="color:red;">`https://NGROK_FORWARDING_URL/webhooks`</span>
- "After Subscribe URL" as <span style="color:red;">`https://NGROK_FORWARDING_URL/Views/AppStore/GetSubscriptions.cshtml`</span><br>

__Few Notes__<br>
Note that you also will have to have a state for checking cross site forgery attacks.
<br>
The <span style="color:red;">CallbackUri</span> has to be exactly the same as redirect URI you put in Xero developer portal letter by letter.
<br>
If Bankfeed and/or Finance API(s) is/are used with the example code, you must contact Xero via https://www.xero.com/uk/partner-programs/financialweb/contact/ for access request.
<br>

### 6. Xero Netstandard SDK
You can either download our SDK from [XeroAPI Github](https://github.com/XeroAPI/Xero-NetStandard) and refer to locally saved file using \<ProjectReference> or use our nuget package online with \<PackageReference> method.<br>
For example, if you choose to use the \<ProjectReference> way, you would set your directories similar to this.
```
<ProjectReference Include="../../../Reference Repos/SDKs/Xero-NetStandard/Xero.NetStandard.OAuth2Client/Xero.NetStandard.OAuth2Client.csproj" Version="1.6.0" />
```
If you choose to reference the Nuget package from online, you would use;
```
<PackageReference Include="Xero.NetStandard.OAuth2" Version="10.0.0" />
```
The version numbers may differ.
<br>
<br>

## Running the App
### Construct the Database
Go <span style="color:red;">'cd'</span> to the directory where the .csproj resides, create the database by running <span style="color:red;">dotnet ef migrations add InitialCreate</span><br>
This creates a <span style="color:red;">Migrations</span> folder with code to build the schema.<br>
Then run <span style="color:red;">dotnet ef database update</span> to execute the migration and create database based on the DbContext and models.<br>
More on this context and models below.<br>


### Build and Run the Project
__With Command Line__<br>
Change directory to XeroDotnetSampleApp directory on your terminal of choice where you can see XeroDotnetSampleApp.csproj, build the project by: 
```
$ dotnet build

Xero-Dotnet-Sample-App succeeded (0.8s) → bin\Debug\net9.0\Xero-Dotnet-Sample-App.dll

Build succeeded with 16 warning(s) in 2.9s
```

Run the project by:
```
$ dotnet run
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Development
info: Microsoft.Hosting.Lifetime[0]
      Content root path: /Users/.../XeroDotnetSampleApp/XeroDotnetSampleApp
```

__With VS Code__<br>
Go back to Explorer and press F5 (or go to _Debug_ > _Start Debugging_). It will ask you for environment for launch configuration. Select _.NET Core_.

![image](https://user-images.githubusercontent.com/41350731/76297100-5d571680-630b-11ea-8ba2-c47105931ff4.png)

Save the launch.json, then press F5 again to run.

![image](https://user-images.githubusercontent.com/41350731/76299273-e3c12780-630e-11ea-9efc-c6460f0fb2ac.png)

You should see following in the DEBUG CONSOLE and be directed to your default browser with https://localhost:5001 already open. 

![image](https://user-images.githubusercontent.com/41350731/76297350-ca6aac00-630b-11ea-8cd3-05f098c3226a.png)

Start your Testing. 
<br>
<br>


## Some Explanation of the Code Folders and Files
### Config Folder
- The folder holds classess for database settings, Sign up with Xero settings, webhook settings and XASS settings
- The classes have variables necessary to be used to link the values stored in the appsettings.json to the program

### Controllers
**HomeController**
- checks if there is a xerotoken.json, and 
- passes a boolean firstTimeConnection to view to control the display of buttons. 

**AuthorizationController**
- reads XeroConfiguration &  make httpClientFactory available via dependency injection
- on /Authorization/, redirects user to Xero OAuth for authentication & authorization
- receives callback on /Authorization/Callback request Xero token
- gets connected tenants (organisations)
- store token via a public static method TokenUtilities.StoreToken(xeroToken);

**BasicAuthorizationController**
- Similar to AuthorizationController except uses special access token to access
- APIs with apps.connection and/or marketplace.billing
- Used to access the subscriptions API in this sample app.

**AppStoreController**
- Along with MeteredBillingController and SignUpWithXeroController this controller takes care of the XASS flow
- If you are using ngrok as this sample app suggests for development purpose, make sure you have set the "After Subscribe URL" from the developer portal to <span style="color:red;">`https://YOUR_NGROK_FORWARDING_URL/Views/AppStore/GetSubscriptions.cshtml`</span>

**AssetsInfoController**
- makes API call to assets endpoint (Asset API)
- displays all current Fixed Assets (GET)
- allows for creation of Fixed Asset (PUT)

**AuEmployeesInfoController**
- makes API call to employees endpoint (PayrollAu API)
- displays all current AU employees (GET)
- allows for creation of a new Employee (PUT)

**BankfeedConnectionsController**
- makes API call to feed connections endpoint (BankFeeds API)
- displays all current feed connections (GET), allows for deletion (POST)
- allows for creation of new feed connection (PUT)

**BankfeedStatementsController**
- makes API call to statements endpoint (BankFeeds API)
- displays all current statements (GET)
- allows for creation of new statement (PUT)

**BankTransactionsInfoController**
- makes API call to bank transactions endpoint (Accounting API)
- displays all current bank transactions (GET)

**ContactInfoController** 
- makes api call to contacts endpoint
- displays in view
- static view Create.cshtml creates a web form and POST contact info to Create() action, and
- makes an create operation to contacts endpoint 

**IdentityInfoController**
- gets the list of tenant connections
- displays tenant information (GET /connections)
- allows user to disconnect a specific tenant (DELETE /connections/{id})

**InvoiceSyncController**
- gets invoices in the last 7 days and displays them in view (GET /invoices)
- allows user to upload attachments to a specific invoice (POST {id}/attachments)

**NzEmployeesInfoController**
- gets a list of employees in NZ Payroll (GET)
- displays them in view
- allows user to create new employees (POST)

**OrganisationInfoController**
- gets the current organisation information (GET)
- displays in view

**ProjectInfoController**
- gets the list of projects in Xero projects (GET)
- displays in view

**PurchaseOrderSyncController**
- gets a list of purchase orders (GET)
- displays in view
- allows user to create a new purchase order (POST)

**UkEmployeeInfoController**
- gets a list of employees in Uk Payroll (GET)
- displays them in view
- allows user to create new employees (POST)

**FilesSyncController**
- makes API call to files endpoint (Files API)
- displays all current files (GET)
- allows for upload of new files (POST)
- can modify existing files (GET /FilesSync/{fileId})
- can delete existing files (PUT)

**FoldersSyncController**
- makes API call to folders endpoint (Files API)
- displays all current folders (GET)
- allows user to create new folders (POST)
- can modify existing folders (GET /FoldersSync/{folderId})
- can delete existing folders (PUT)

**AssociationsSyncController**
- makes API call to associations endpoint (Files API)
- displays all current associations (GET)
- allows user to create new associations (POST)
- can delete existing folders (PUT)

**WebhookController**
- receive the webhook and process according to the event category and type
- have your "Delivery URL" set as <span style="color:red;">`https://NGROK_FORWARDING_URL/webhooks`</span>

### Data Folder
- Holds the user context model creation function that gets executed when the SQLite database is created as the program runs
- It will use the SignUpWithXeroUser.cs file in the Models folder which holds the class definition of a referral user to create the database table

### DTO Folder
- The folder relevant enum types and variables needed to identify and process webhooks

### IO Folder
- Holds class and interface files needed to interact with Xero tokens
- For example, ITokenIO.cs is the interface file tied to LocalStorageTokenIO.cs class to access the functions
- The non-tenanted versions are used to retrieve non-tenanted API calls for such as getting the connections established to the app and getting subscriptions list

### Migrations Folder
- Is generated as an initial step of creating the database using the Microsoft Entityframework and SQLite3

### Views Folder
- Holds the cshtml views needed to create the UI of the web application

### Tokens
Xero token is stored in a JSON file in the root of the project "./xerotoken.json".<br>
The app serialise and deserialise with the static class functions in /Utilities/TokenUtilities.cs.<br>
Most controllers will get and refresh token before calling API methods.
<br><br>
Similarly the Non-tenanted token is stored in a JSON file in the root of the project "./non_tenanted_xerotoken.json".<br>
The difference is that this token is auto generated during the Get Subscriptions function call in AppStoreController.cs file when var xeroNonTenantedToken = (XeroOAuth2Token) await client.RequestClientCredentialsTokenAsync(false); is called.<br>
<br>
<br>

## Cross Site Forgery Attack Example
For demonstrating OAuth 2.0 [CSFR](https://auth0.com/docs/protocols/state-parameters) implementation, two static methods were created to handle local storage of current state (state.json): TokenUtilities.StoreState(string state) and TokenUtilities.GetCurrentState(). 

In AuthenticationController, on construction it generates a random GUID string as state. The Index() will store the state to state.json, then be retrieved on Callback(). If state does not match, the controller returns a warning "Cross site forgery attack detected!" instead of carrying forward the token request flow.
<br>
<br>

## License

This software is published under the [MIT License](http://en.wikipedia.org/wiki/MIT_License).

	Copyright (c) 2025 Xero Limited

	Permission is hereby granted, free of charge, to any person
	obtaining a copy of this software and associated documentation
	files (the "Software"), to deal in the Software without
	restriction, including without limitation the rights to use,
	copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the
	Software is furnished to do so, subject to the following
	conditions:

	The above copyright notice and this permission notice shall be
	included in all copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
	EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
	OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
	NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
	HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
	WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
	FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
	OTHER DEALINGS IN THE SOFTWARE.