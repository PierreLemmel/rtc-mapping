@echo off
SET LOCAL_IP=192.168.1.104
SET NGINX_DIR=C:\nginx
SET CONF_SOURCE=nginx.conf
SET CONF_DEST=%NGINX_DIR%\conf\nginx.conf
SET CERT_FILE=%NGINX_DIR%\ssl\nginx-selfsigned.crt
SET KEY_FILE=%NGINX_DIR%\ssl\nginx-selfsigned.key

echo [1/6] Copying nginx.conf to %NGINX_DIR%\conf...
copy /Y "%CONF_SOURCE%" "%CONF_DEST%"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to copy nginx.conf. Ensure %NGINX_DIR%\conf exists.
    pause
    exit /b 1
)

echo [2/6] Checking for certificates...
if not exist "%CERT_FILE%" (
    echo ERROR: Certificate file not found at %CERT_FILE%
    pause
    exit /b 1
)
if not exist "%KEY_FILE%" (
    echo ERROR: Key file not found at %KEY_FILE%
    pause
    exit /b 1
)

echo [3/6] Stopping Nginx if running...
taskkill /IM nginx.exe /F >nul 2>&1

echo [4/6] Testing Nginx configuration...
cd /d %NGINX_DIR%
nginx -t
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Nginx configuration is invalid. Check the output above.
    pause
    exit /b 1
)

echo [5/6] Starting Nginx...
start nginx
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to start Nginx.
    pause
    exit /b 1
)

echo [6/6] SUCCESS: Nginx is running and configured as a reverse proxy.
echo You can now access the app at https://%LOCAL_IP%
pause

