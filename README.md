
# Health Monitor Application

This web application is designed to monitor the health of various API endpoints. It allows users to upload OpenAPI documents, generate cURL commands, perform health checks on endpoints, analyze repositories, and manage application settings. The application also supports dynamic string processing and custom assertions using C# scripts.

## Table of Contents
1. [Installation](#installation)
2. [Configuration](#configuration)
3. [Using the Application](#using-the-application)
    - [Dashboard](#dashboard)
    - [Uploading OpenAPI Documents](#uploading-openapi-documents)
    - [Managing cURL Commands](#managing-curl-commands)
    - [Repository Analysis](#repository-analysis)
    - [Application Settings](#application-settings)
    - [Custom Assertions and Dynamic Strings](#custom-assertions-and-dynamic-strings)
4. [Error Handling](#error-handling)
5. [Security Considerations](#security-considerations)

---

## Installation

1. **Clone the Repository:**
   ```bash
   git clone https://github.com/msbel5/HealthMonitorApp.git
   ```
2. **Navigate to the Project Directory:**
   ```bash
   cd HealthMonitorApp
   ```
3. **Install Dependencies:**
   Make sure to restore NuGet packages if required:
   ```bash
   dotnet restore
   ```
4. **Build the Application:**
   ```bash
   dotnet build
   ```
5. **Run the Application:**
   ```bash
   dotnet run
   ```

## Configuration

Before using the application, certain settings need to be configured:

1. **Database:**
   Ensure the database connection is set up in the `appsettings.json` file.

2. **SMTP Settings:**
   Configure the SMTP server details in the Settings section of the web application to enable email notifications.

3. **API Key for SendGrid (if used):**
   Update the SendGrid API key in the appropriate section of the application for email notifications.

## Using the Application

### Dashboard

- **View Service Health:**
  The main dashboard provides a visual overview of the health of monitored services. Green, yellow, and red icons indicate healthy, slow, and unhealthy services, respectively.

- **Check All Endpoints:**
  Click the "Check All Endpoints" button to manually trigger health checks for all services.

- **Add New Service Status:**
  Use the "Create New Service Status" button to add a new API endpoint to be monitored.

### Uploading OpenAPI Documents

- **Upload OpenAPI Document:**
  On the home page, there is a button in the layout that allows users to upload an OpenAPI JSON file. This will generate cURL commands for the API endpoints described in the document.

- **View Generated Scripts:**
  After uploading, users are redirected to the "Result" page where they can view, edit, and save the generated cURL commands.

### Managing cURL Commands

- **Edit cURL Commands:**
  On the "Result" page, users can edit cURL commands to include dynamic strings or customize headers and parameters.

- **Dynamic String Processing:**
  Use `${{ }}` syntax to include JavaScript code within your cURL commands. This code is executed at runtime, allowing you to dynamically adjust command parameters.

### Repository Analysis

- **Analyze Repositories:**
  In the "Analysis" section, view the list of repositories that have been analyzed. You can add new repositories or manage existing ones.

- **Details and Actions:**
  For each repository, you can view details, edit settings, or delete the entry. The "Check All Repositories" button allows for batch analysis.

### Application Settings

- **Configure Health Check Intervals:**
  Set the interval for how often health checks should be performed.

- **Notification Emails:**
  Manage the list of email addresses that will receive notifications if a service becomes unhealthy.

- **SMTP Configuration:**
  Enter the details for your SMTP server, including server address, port, username, and password.

### Custom Assertions and Dynamic Strings

- **Custom Assertions:**
  Write custom C# scripts to validate API responses. Access predefined scripts and guidance using the Assertion Help Modal.

- **Dynamic Strings:**
  Include dynamic content in your cURL commands using JavaScript. Use the Dynamic String Help Modal to test and understand how dynamic strings work.

## Error Handling

- **User-Friendly Error Pages:**
  If something goes wrong, the application will display an error page with helpful information.

- **Logging:**
  Errors are logged to assist in diagnosing issues with health checks, script execution, or other processes.

## Security Considerations

- **Sensitive Data Encryption:**
  Ensure that sensitive data, like SMTP passwords, are securely stored and encrypted.

- **API Keys Management:**
  Store API keys securely, ideally in environment variables, and not directly in the codebase.
