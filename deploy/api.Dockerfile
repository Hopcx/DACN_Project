# T20: image API DACN (ASP.NET Core 8). Build từ thư mục gốc repo:
#   docker build -f deploy/api.Dockerfile -t dacn-api .
# Cấu hình (chuỗi kết nối, khóa JWT, SMTP, media…) truyền bằng biến môi trường lúc chạy — không có secret nào trong image.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Khôi phục gói trước (tận dụng cache khi chỉ đổi mã nguồn)
COPY DACN_Project.sln ./
COPY Project.Api/Project.Api.csproj Project.Api/
COPY Project.Application/Project.Application.csproj Project.Application/
COPY Project.Domain/Project.Domain.csproj Project.Domain/
COPY Project.Infrastructure/Project.Infrastructure.csproj Project.Infrastructure/
RUN dotnet restore Project.Api/Project.Api.csproj
COPY Project.Api/ Project.Api/
COPY Project.Application/ Project.Application/
COPY Project.Domain/ Project.Domain/
COPY Project.Infrastructure/ Project.Infrastructure/
RUN dotnet publish Project.Api/Project.Api.csproj -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
# curl chỉ để healthcheck của Docker gọi /web/health/ready
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out ./
# Thư mục ghi được cho user không phải root: log (Serilog logs/), media, khóa DataProtection (volume)
RUN mkdir -p /app/logs /var/lib/dacn/media /var/lib/dacn/dp-keys \
 && chown -R $APP_UID:$APP_UID /app/logs /var/lib/dacn
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Media__RootPath=/var/lib/dacn/media \
    Proxy__Enabled=true
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD curl -fsS http://localhost:8080/web/health/ready || exit 1
ENTRYPOINT ["dotnet", "Project.Api.dll"]
