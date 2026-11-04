# ── Build ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj trước để cache layer restore (chỉ restore lại khi đổi package)
COPY RenTrek.sln ./
COPY global.json Directory.Build.props .editorconfig ./
COPY src/RenTrek.API/RenTrek.API.csproj src/RenTrek.API/
COPY src/RenTrek.BLL/RenTrek.BLL.csproj src/RenTrek.BLL/
COPY src/RenTrek.DAL/RenTrek.DAL.csproj src/RenTrek.DAL/
COPY src/RenTrek.API/packages.lock.json src/RenTrek.API/
COPY src/RenTrek.BLL/packages.lock.json src/RenTrek.BLL/
COPY src/RenTrek.DAL/packages.lock.json src/RenTrek.DAL/
RUN dotnet restore src/RenTrek.API/RenTrek.API.csproj --locked-mode

COPY src/ src/
RUN dotnet publish src/RenTrek.API/RenTrek.API.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ── Runtime ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

# Chạy bằng user không phải root (có sẵn trong image aspnet)
USER $APP_UID

EXPOSE 8080

# Render cấp cổng qua biến PORT; chạy local mặc định 8080. TLS do Render xử lý.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet RenTrek.API.dll"]
