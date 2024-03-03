# Health Monitor Web Application

## Introduction
This repository contains the Health Monitor Web Application, a tool designed for monitoring the health status of various services. The application provides a user-friendly interface to add, view, and manage service statuses, along with their corresponding cURL commands and expected response times.

## Features
- **Service Status Management**: Add, view, and edit service statuses.
- **Dynamic cURL Command Input**: Users can dynamically add multiple cURL commands for each service.
- **API Group Management**: Select existing API groups or add new ones.
- **Health Status Visualization**: Visual indicators (green, yellow, red) show the health status of services.
- **Responsive Design**: The application is designed to be responsive and user-friendly.

## Installation
1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/health-monitor-app.git
   ```
2. Navigate to the project directory:
   ```bash
   cd health-monitor-app
   ```
3. Install dependencies (assuming you are using a .NET environment):
   ```bash
   dotnet restore
   ```
4. Run the application:
   ```bash
   dotnet run
   ```

## Usage
- **Adding a Service Status**: Navigate to the "Create Service Status" page to add a new service status. Fill in the details like service name, expected status code, and cURL commands.
- **Editing a Service Status**: Use the edit icons in the service list to modify existing service statuses.
- **Viewing Service Health**: The main dashboard displays the health status of all services in either circle or line views.
