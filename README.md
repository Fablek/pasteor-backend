# Pasteor Backend

Backend API for Pasteor - A modern pastebin alternative built with ASP.NET Core and PostgreSQL.

## 🚀 Features

- **RESTful API** with comprehensive endpoints for pastes, comments, and authentication
- **Multiple Authentication Methods**:
  - Google OAuth 2.0
  - GitHub OAuth 2.0
  - Local authentication (email/password with BCrypt)
- **JWT Token-based Authorization**
- **PostgreSQL Database** with Entity Framework Core
- **Automatic Paste Expiration** (1h, 24h, 7d, 30d, or never)
- **Comment System** for authenticated and anonymous users
- **User Statistics** and analytics
- **Swagger/OpenAPI Documentation**
- **CORS Configuration** for frontend integration
- **Global Exception Handling**

## 🛠️ Tech Stack

- **.NET 9.0** - Modern web framework
- **ASP.NET Core** - Web API framework
- **Entity Framework Core 9.0** - ORM
- **PostgreSQL 16** - Primary database
- **JWT Bearer** - Authentication
- **BCrypt.Net** - Password hashing
- **Swagger/Swashbuckle** - API documentation
- **Docker** - PostgreSQL containerization

## 📋 Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [PostgreSQL 16](https://www.postgresql.org/download/) or [Docker](https://www.docker.com/)
- [Git](https://git-scm.com/)

## 🔧 Installation

### 1. Clone the repository

```bash
git clone https://github.com/Fablek/pasteor-backend.git
cd pasteor-backend
```

### 2. Set up PostgreSQL

#### Option A: Using Docker (Recommended)

```bash
docker-compose up -d
```

This will start PostgreSQL on port 5432 with:
- Database: `pasteordb`
- Username: `pasteor`
- Password: `pasteor123`

#### Option B: Using Local PostgreSQL

Install PostgreSQL and create a database:

```sql
CREATE DATABASE pasteordb;
CREATE USER pasteor WITH PASSWORD 'pasteor123';
GRANT ALL PRIVILEGES ON DATABASE pasteordb TO pasteor;
```

### 3. Configure Application Settings

Update `appsettings.json` or create `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=pasteordb;Username=pasteor;Password=pasteor123"
  },
  "Authentication": {
    "Jwt": {
      "Key": "your-secret-key-at-least-32-characters-long",
      "Issuer": "pasteor",
      "Audience": "pasteor-frontend"
    },
    "Google": {
      "ClientId": "your-google-client-id",
      "ClientSecret": "your-google-client-secret"
    },
    "GitHub": {
      "ClientId": "your-github-client-id",
      "ClientSecret": "your-github-client-secret"
    }
  }
}
```

#### Getting OAuth Credentials

**Google OAuth:**
1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing
3. Enable Google+ API
4. Create OAuth 2.0 credentials
5. Add authorized redirect URI: `http://localhost:5297/api/auth/google-callback`

**GitHub OAuth:**
1. Go to [GitHub Developer Settings](https://github.com/settings/developers)
2. Create a new OAuth App
3. Set Authorization callback URL: `http://localhost:5297/api/auth/github-callback`

### 4. Apply Database Migrations

```bash
dotnet ef database update
```

### 5. Run the Application

```bash
dotnet run
```

The API will be available at:
- HTTP: `http://localhost:5297`
- Swagger UI: `http://localhost:5297/swagger`

## 📚 API Documentation

Once the application is running, visit the Swagger UI at `http://localhost:5297/swagger` for interactive API documentation.

### Main Endpoints

#### Authentication
- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Login with email/password
- `GET /api/auth/google` - Initiate Google OAuth
- `GET /api/auth/github` - Initiate GitHub OAuth
- `GET /api/auth/me` - Get current user info

#### Pastes
- `POST /api/pastes` - Create new paste
- `GET /api/pastes/{id}` - Get paste by ID
- `GET /api/pastes/{id}/raw` - Get raw paste content
- `GET /api/pastes/recent` - Get recent pastes
- `GET /api/pastes/public-stats` - Get public statistics
- `GET /api/pastes/my` - Get user's pastes (auth required)
- `GET /api/pastes/stats` - Get user statistics (auth required)
- `GET /api/pastes/languages` - Get user's languages (auth required)
- `PUT /api/pastes/{id}` - Update paste (auth required)
- `DELETE /api/pastes/{id}` - Delete paste (auth required)

#### Comments
- `GET /api/comments/paste/{pasteId}` - Get comments for paste
- `POST /api/comments` - Create comment
- `DELETE /api/comments/{id}` - Delete comment (auth required)

## 🗂️ Project Structure

```
pasteor-backend/
├── Controllers/           # API Controllers
│   ├── AuthController.cs     # Authentication endpoints
│   ├── PastesController.cs   # Paste CRUD operations
│   └── CommentsController.cs # Comment operations
├── Data/                  # Database context
│   └── ApplicationDbContext.cs
├── Models/                # Data models
│   ├── User.cs
│   ├── Paste.cs
│   └── Comment.cs
├── Services/              # Business logic
│   └── JwtService.cs         # JWT token generation
├── Middleware/            # Custom middleware
│   └── GlobalExceptionHandler.cs
├── Migrations/            # EF Core migrations
├── Program.cs             # Application entry point
├── appsettings.json       # Configuration
└── docker-compose.yml     # PostgreSQL setup
```

## 🗃️ Database Schema

### User
- `Id` (int, PK)
- `Email` (string, unique)
- `Name` (string, nullable)
- `AvatarUrl` (string, nullable)
- `Provider` (string) - "Google", "GitHub", or "Local"
- `ProviderId` (string, nullable)
- `PasswordHash` (string, nullable)
- `CreatedAt` (DateTime)

### Paste
- `Id` (string, PK) - 8-character unique identifier
- `Content` (string) - Max 512KB
- `Title` (string, nullable)
- `Language` (string) - Default: "plaintext"
- `CreatedAt` (DateTime)
- `ExpiresAt` (DateTime, nullable)
- `Views` (int)
- `CreatedByIp` (string, nullable)
- `UserId` (int, FK, nullable)

### Comment
- `Id` (int, PK)
- `PasteId` (string, FK)
- `UserId` (int, FK, nullable)
- `Content` (string) - Max 2000 characters
- `AuthorName` (string, nullable) - For anonymous users
- `CreatedAt` (DateTime)

## 🔒 Security Features

- **BCrypt Password Hashing** - Secure password storage
- **JWT Authentication** - Stateless token-based auth
- **CORS Policy** - Restricted to frontend origin
- **Input Validation** - Request validation on all endpoints
- **Content Size Limits** - 512KB for pastes, 2000 chars for comments
- **Authorization Checks** - Resource ownership verification
- **HttpOnly Cookies** - Secure session management
- **Data Protection API** - ASP.NET Core data protection

## 🧪 Development

### Running Migrations

Create a new migration:
```bash
dotnet ef migrations add MigrationName
```

Apply migrations:
```bash
dotnet ef database update
```

Rollback migration:
```bash
dotnet ef database update PreviousMigrationName
```

### Testing with Swagger

1. Start the application
2. Navigate to `http://localhost:5297/swagger`
3. Use "Try it out" feature to test endpoints
4. For authenticated endpoints, use the "Authorize" button with JWT token

### Environment Variables

You can use environment variables instead of `appsettings.json`:

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Database=pasteordb;..."
export Authentication__Jwt__Key="your-secret-key"
dotnet run
```

## 🐳 Docker

### Build Docker Image

```bash
docker build -t pasteor-backend .
```

### Run with Docker Compose

```bash
docker-compose up
```

## 📝 Notes

- Default paste expiration options: `1h`, `24h`, `7d`, `30d`, `never`
- Paste IDs are 8-character alphanumeric strings
- Anonymous users can create pastes and comments
- Views are not counted for paste owners
- Expired pastes return 404 when accessed

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License.

## 🔗 Related Projects

- [Pasteor Frontend](https://github.com/Fablek/pasteor-frontend) - Next.js frontend application

## 📧 Contact

Project Link: [https://github.com/Fablek/pasteor-backend](https://github.com/Fablek/pasteor-backend)

---

Made with ❤️ using ASP.NET Core
