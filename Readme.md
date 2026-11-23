# Dormitory Management System
### Database Design & .NET Development

<p align="center">
  ![.NET](https://img.shields.io/badge/.NET-512BD4?logo=dotnet&logoColor=white)
  ![SQL Server](https://img.shields.io/badge/Microsoft_SQL_Server-CC2927?logo=microsoftsqlserver&logoColor=white)
  ![Database](https://img.shields.io/badge/Database_Design-SQL-blue)
  ![Status](https://img.shields.io/badge/Project-Academic-success)
</p>

## Overview

**Dormitory Management System** is a comprehensive software project designed to manage and automate the major administrative and operational processes of student dormitories.

The system provides a centralized platform for managing dormitory facilities, student information, room assignments, group reservations, payments, access logs, maintenance requests, and announcements.

The project combines relational database design and SQL Server development with a .NET-based application layer, aiming to provide an organized and role-oriented management solution.

## System Roles

The system supports four distinct user roles, each with specific permissions and responsibilities:

| Role | Responsibilities |
|---|---|
| Dormitory Manager | Overall dormitory administration and system management |
| Block Supervisor | Management of assigned blocks, students, and maintenance requests |
| Security Guard | Student information lookup and access log monitoring |
| Student | Group formation, room reservations, payments, and personal requests |

## Core Features

### 1. Dormitory Infrastructure Management

- Management of multiple dormitories and their physical structures.
- Creation and administration of blocks and rooms.
- Tracking room capacity, availability, and physical condition.
- Management of shared facilities, including study halls and sports halls.

### 2. Student Management

- Student registration and profile management.
- Storage of academic, personal, and emergency contact information.
- Student search and filtering.
- Parent and emergency contact management.

### 3. Group Formation & Room Reservation

- Student group creation and management.
- Automatic gender compatibility validation.
- Group leader identification.
- Time-restricted registration and room selection.
- Automatic room availability and capacity checking.
- Individual and group-based room assignments.
- Room reservation cancellation and reassignment.

### 4. Payment Management

- Automatic tuition calculation based on dormitory blocks.
- Payment transaction registration.
- Payment status tracking.
- Identification of unpaid students.
- Automated payment reminder announcements.

### 5. Access Control & Security

- Student entry and exit logging.
- Recording access dates and timestamps.
- Validation of consecutive entry and exit records.
- Access history filtering and reporting.

### 6. Maintenance & Reporting

- Student maintenance request submission.
- Categorization and description of reported issues.
- Assignment of requests to responsible supervisors.
- Administrative reporting and tracking.

### 7. Announcement Management

- Creation and distribution of announcements.
- Targeted announcements for individual students, blocks, or dormitories.
- Multiple delivery methods, including system notifications, SMS, and email.

## Database Design

The database is designed using **Microsoft SQL Server** and a relational data model.

The schema incorporates multiple related entities and implements referential integrity through primary keys, foreign keys, unique constraints, and check constraints.

Major database entities include:

- Student and Staff
- Dormitory, Block, and Room
- Group and TimeSlot
- Payment and AccessLog
- Announcement and Report
- Parent
- StudyHall and SportHall
- ManagesBlock
- Announcement recipient relationship tables

### Database Concepts & Implementation

The project demonstrates practical database engineering concepts, including:

- Relational database modeling
- Entity-Relationship (ER/EER) design
- Primary and Foreign Keys
- Referential Integrity
- CHECK and UNIQUE Constraints
- SQL Joins and Subqueries
- Views
- Triggers
- Stored Procedures
- Cursor-based processing
- Data manipulation and transaction workflows

### Advanced SQL Implementation

One of the implemented stored procedures is `NotifyUnpaidStudents`.

This procedure uses a SQL Server cursor to iterate through pending payment records and automatically generate payment reminder announcements for the corresponding students.

The database also includes trigger-based validation for student access logs to prevent invalid consecutive entry or exit records.

## Application & Technology Stack

| Technology | Purpose |
|---|---|
| C# / .NET | Application development |
| Microsoft SQL Server | Relational database management |
| T-SQL | Database schema, queries, and logic |
| Visual Studio | Development environment |

## Repository Structure

```text
Dormitory-Management-System/
│
├── Backend/
│   └── .NET Application
│
├── Database/
│   ├── Schema/
│   ├── Tables/
│   ├── Constraints/
│   ├── Triggers/
│   ├── Procedures/
│   ├── Views/
│   ├── Functions/
│   ├── Queries/
│   └── SampleData/
│
├── Documentation/
│   ├── ER_Diagram.pdf
│   └── Project_Report.pdf
│
└── README.md
```

## Getting Started

### Prerequisites

- Microsoft SQL Server
- SQL Server Management Studio (SSMS)
- .NET SDK
- Visual Studio

### Database Setup

1. Clone the repository.
2. Open SQL Server Management Studio.
3. Execute the database creation scripts in the appropriate order.
4. Create the tables, constraints, views, triggers, and stored procedures.
5. Execute the sample data scripts to populate the database.

### Application Setup

1. Open the `.sln` solution file in Visual Studio.
2. Configure the database connection string using your local SQL Server instance.
3. Restore the required NuGet packages.
4. Build and run the application.

## Documentation

The `Documentation` directory contains the database design diagrams and project documentation, including the ER/EER model and detailed descriptions of system functionalities and SQL implementations.

## Authors

**Atabak Abroshan**

**Mania Jalali**

Iran University of Science and Technology (IUST)

---

<p align="center">
  <i>Designed and developed as an academic Database Systems project.</i>
</p>