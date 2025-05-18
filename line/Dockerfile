FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# คัดลอกไฟล์โปรเจกต์ และ restore dependencies
COPY ["line.csproj", "./"]
RUN dotnet restore "./line.csproj"

# คัดลอกไฟล์ทั้งหมดและ build
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# คัดลอกไฟล์ที่ build มาแล้วจาก build stage
COPY --from=build /app/publish .

# รันไฟล์ DLL ด้วย dotnet
ENTRYPOINT ["dotnet", "line.dll"]
