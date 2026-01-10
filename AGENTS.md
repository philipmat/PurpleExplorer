# Agent Guide for PurpleExplorer

This document provides essential information for AI agents and developers working on the PurpleExplorer project.

## Project Overview
PurpleExplorer is a cross-platform desktop application for managing Azure Service Bus. It allows users to view topics, subscriptions, queues, and messages, as well as send, delete, and resubmit messages.

## Tech Stack
- **Framework:** [Avalonia UI](https://avaloniaui.net/) (Cross-platform UI framework for .NET)
- **Pattern:** MVVM (Model-View-ViewModel) using [ReactiveUI](https://www.reactiveui.net/)
- **Runtime:** .NET 8.0
- **Azure SDK:** `Azure.Messaging.ServiceBus`

## Project Structure
- `PurpleExplorer/`: Main project directory.
    - `Assets/`: Icons and static assets.
    - `Helpers/`: Core logic for interacting with Azure Service Bus and UI utilities.
        - `TopicHelper.cs`: Logic for topics and subscriptions.
        - `QueueHelper.cs`: Logic for queues.
    - `Models/`: Data contracts and state models.
        - `AppState.cs`: Defines the application's persistent state.
        - `AppSettings.cs`: User-configurable settings.
    - `Services/`: Application services (e.g., Logging).
    - `Styles/`: XAML styles for the Avalonia UI.
    - `ViewModels/`: UI logic and data binding.
        - `MainWindowViewModel.cs`: The primary ViewModel for the main window.
    - `Views/`: XAML files for the UI.
        - `MainWindow.xaml`: The main application window.

## Key Components & Responsibilities
### ViewModels
- **MainWindowViewModel**: Coordinates most of the application's actions, including fetching resources, managing selections, and triggering operations like purging or transferring messages.

### Helpers
- **TopicHelper / QueueHelper**: These classes wrap the Azure Service Bus SDK. They handle the low-level details of connecting to Azure, peeking messages, and performing management operations.

### State Management
- **AppState**: The application state is persisted in `appstate.json` (located in the project root or the executable directory). It stores saved connection strings, saved messages, and user settings.
- **NewtonSoftJsonSuspensionDriver**: Handles the serialization and deserialization of the application state.

## Important Notes for Agents
- **Destructive Actions**: Actions like "Delete Message" or "Purge" have significant consequences. Ensure the user is aware of the risks (as noted in the README regarding `DeliveryCount`).
- **Azure SDK**: The project uses `Azure.Messaging.ServiceBus`. 
- **Tests**: Currently, the project lacks automated tests. When adding new features, consider adding unit tests in a separate test project (e.g., `PurpleExplorer.Tests`).

## Development Workflow
- **Running the app:** Use `dotnet run --project PurpleExplorer/PurpleExplorer.csproj`.
- **Building:** `dotnet build`.
- **Formatting:** Follow the existing C# coding style (standard .NET conventions).
    - **Order of class members**: The order of class members should follow a logical sequence, starting with fields (constants, then readonly, then instance),
      then constructors, followed by properties, and finally methods.  
      - Within each category, the order is: public, protected, internal, private.
      - There should be one blank line between each category.
      - There should be no public fields.
      - This makes the code easier to read and understand.