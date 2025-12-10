# Nginx File Upload Fix - Deployment Instructions

## Problem
Nginx was rejecting file uploads larger than 1MB with error:
```
client intended to send too large body: 9881296 bytes
```

## Root Cause
The `.platform` folder approach was **NOT being deployed** to your EC2 instance. The EB logs showed:
```
[INFO] The dir .platform/hooks/postdeploy/ does not exist
```

## Solution
We switched to the **`.ebextensions` approach** which is proven to work (your HTTPS redirect already uses this method).

## What Was Changed

### 1. `.ebextensions/00_nginx_file_upload.config` (NEW FILE)
This file writes directly to `/etc/nginx/conf.d/` and reloads nginx after deployment.
- Sets `client_max_body_size 15M`
- Configures timeouts and proxy settings
- Reloads nginx automatically

### 2. Updated All Application Limits to 15MB
- **Kestrel**: `MaxRequestBodySize = 15728640` (15 MB)
- **Form Options**: `MultipartBodyLengthLimit = 15728640` (15 MB)
- **Controller**: `[RequestSizeLimit(15728640)]` on upload endpoint
- **Nginx**: `client_max_body_size 15M`

## Files Modified

### Configuration Files
- ✅ `.ebextensions/00_nginx_file_upload.config` (created)
- ✅ `.platform/nginx/conf.d/client_max_body_size.conf` (backup, but may not deploy)
- ✅ `src/TutoriaApi.Web.API/Program.cs` (Kestrel + Form limits)
- ✅ `src/TutoriaApi.Web.API/Controllers/FilesController.cs` (controller attributes)

### Removed Files
- ❌ `.platform/hooks/postdeploy/01_configure_nginx.sh` (not deployed)
- ❌ `.ebextensions/99_verify_nginx.config` (not needed)

## Deployment Steps

### 1. Commit Changes
```bash
cd TutoriaApi
git add .
git commit -m "fix: configure nginx for 15MB file uploads via ebextensions"
git push
```

### 2. Deploy to Elastic Beanstalk
Your CI/CD pipeline should automatically deploy, or use:
```bash
# If using EB CLI
eb deploy

# If manual deployment
dotnet publish src/TutoriaApi.Web.API/TutoriaApi.Web.API.csproj -c Release -o ./publish
cd publish
zip -r ../deploy.zip .
# Upload deploy.zip to EB console
```

### 3. Verify Deployment

After deployment, SSH into the EB instance and verify:

```bash
# SSH into instance
eb ssh

# Check if config was created
cat /etc/nginx/conf.d/00_file_upload_size.conf
# Should show: client_max_body_size 15M;

# Search for all nginx configs
grep -r "client_max_body_size" /etc/nginx/
# Should show 15M (not 1m)

# Test nginx config
sudo nginx -t

# Check nginx is running
sudo systemctl status nginx
```

### 4. Test File Upload

Try uploading a file ~10-14MB via your frontend:
- File should upload successfully
- No more "413 Request Entity Too Large" errors
- No more "client intended to send too large body" errors in logs

## Why This Approach Works

1. **`.ebextensions` files are ALWAYS deployed** by Elastic Beanstalk
2. **Your HTTPS redirect already uses this method** (proven to work)
3. **Files are loaded in alphabetical order** (`00_` loads before `01_` and `https-redirect.config`)
4. **Nginx reloads automatically** via `container_commands`
5. **No dependency on `.platform` folder deployment**

## Troubleshooting

### If upload still fails after deployment:

1. **Check EB logs** for nginx configuration errors:
   ```bash
   eb logs
   # Look for nginx errors or "client intended to send too large body"
   ```

2. **Verify file was created**:
   ```bash
   eb ssh
   ls -la /etc/nginx/conf.d/
   cat /etc/nginx/conf.d/00_file_upload_size.conf
   ```

3. **Check nginx error logs**:
   ```bash
   eb ssh
   sudo tail -f /var/log/nginx/error.log
   # Then try uploading a file
   ```

4. **Manually reload nginx** (if needed):
   ```bash
   eb ssh
   sudo systemctl reload nginx
   ```

## Current Limits (All Set to 15MB)

| Layer | Limit | File/Line |
|-------|-------|-----------|
| **Nginx** | 15 MB | `.ebextensions/00_nginx_file_upload.config:9` |
| **Kestrel** | 15 MB | `Program.cs:18` |
| **Form Options** | 15 MB | `Program.cs:43-45` |
| **Controller** | 15 MB | `FilesController.cs:160-161` |

## Next Steps

1. ✅ **Commit and push changes**
2. ✅ **Deploy to EB**
3. ✅ **Verify nginx config is applied** (SSH into instance)
4. ✅ **Test file upload** with ~10-14MB file
5. ✅ **Monitor logs** for any errors

---

**Last Updated**: December 10, 2025
**Max File Size**: 15 MB
**Approach**: `.ebextensions` (reliable deployment)
