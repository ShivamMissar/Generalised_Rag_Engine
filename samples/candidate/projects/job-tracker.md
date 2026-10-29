# Job Tracker

A personal web app for tracking job applications, built to practise full-stack development.

## What it does

Users add applications with the company, role, status, and dates, then move each one through stages from "applied" to "offer". A dashboard shows how many applications are at each stage and how long responses take.

## How it's built

- Backend: ASP.NET Core minimal API in C#, with Entity Framework Core and SQLite.
- Frontend: React with TypeScript, using React Query to fetch data.
- Authentication with ASP.NET Core Identity and JWT tokens.
- Deployed in a Docker container to Azure App Service.

## What I learned

Designing an API before the UI saved a lot of rework. Writing integration tests against a real SQLite database caught bugs that unit tests missed.
