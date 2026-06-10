# Medicare Backend - Agent Guide

## Systemd Service Management

The backend runs as a systemd service called `medicare-backend`.

### Service Details
- **Service file**: `/etc/systemd/system/medicare-backend.service`
- **Deployed app**: `/opt/medicare-backend/`
- **Listening on**: `http://localhost:5002`
- **Environment**: Production

### Common Commands

```bash
# Check service status
sudo systemctl status medicare-backend

# Restart the service (after deploying new code)
sudo systemctl restart medicare-backend

# Stop the service
sudo systemctl stop medicare-backend

# Start the service
sudo systemctl start medicare-backend

# View live logs (follow mode)
sudo journalctl -u medicare-backend -f

# View recent logs (last 100 lines)
sudo journalctl -u medicare-backend -n 100

# View logs since last boot
sudo journalctl -u medicare-backend -b

# View logs with timestamps
sudo journalctl -u medicare-backend --output=cat
```

## Pushing Changes to Running Backend

When the user asks to push/deploy changes to the running backend, follow these steps:

### 1. Build the Release
```bash
cd /home/ubuntu/medicare-backend
dotnet publish -c Release -o ./publish
```

### 2. Backup Current Deployment (optional but recommended)
```bash
sudo cp /opt/medicare-backend/MedicalApp.API.dll /opt/medicare-backend/MedicalApp.API.dll.bak
```

### 3. Copy Published Files to Deployment Directory
```bash
sudo cp -r ./publish/* /opt/medicare-backend/
```

### 4. Restart the Service
```bash
sudo systemctl restart medicare-backend
```

### 5. Verify It's Running
```bash
sudo systemctl status medicare-backend
curl -s http://localhost:5002/health || curl -s http://localhost:5002/
```

### One-Liner (Quick Deploy)
```bash
cd /home/ubuntu/medicare-backend && dotnet publish -c Release -o ./publish && sudo cp -r ./publish/* /opt/medicare-backend/ && sudo systemctl restart medicare-backend && sudo systemctl status medicare-backend
```

## Project Structure

- **Framework**: .NET 8 / ASP.NET Core
- **Database**: SQL Server (via Entity Framework Core)
- **Entry point**: `Program.cs`
- **Controllers**: `Controllers/`
- **Services**: `Services/`
- **Data/EF**: `Data/`
- **DTOs**: `DTOs/`
- **Models**: `Models/`
- **Migrations**: `Migrations/`

## Environment Configuration

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Local dev overrides
- Production environment variable: `ASPNETCORE_ENVIRONMENT=Production`
