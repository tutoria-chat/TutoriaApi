# Elastic Beanstalk Platform Configuration

This directory contains platform-specific configuration for AWS Elastic Beanstalk deployment.

## Nginx Configuration Files

### Purpose
These configuration files resolve the "client intended to send too large body" error that occurs when uploading files larger than nginx's default 1MB limit.

### Files

#### `.platform/nginx/conf.d/client_max_body_size.conf`
Sets the maximum allowed request body size to 50MB to support file uploads.

```nginx
client_max_body_size 50M;
```

#### `.platform/nginx/conf.d/proxy_settings.conf`
Configures proxy timeouts to prevent connection drops during large file uploads.

```nginx
proxy_connect_timeout 300;
proxy_send_timeout 300;
proxy_read_timeout 300;
send_timeout 300;
```

## How It Works

When deployed to Elastic Beanstalk:

1. **Nginx** (reverse proxy) receives the upload request first
2. Nginx checks if the request body size is within the allowed limit (50MB)
3. If accepted, Nginx forwards the request to the ASP.NET Core application
4. **Kestrel** (ASP.NET Core web server) processes the request
5. The application handles the file upload through Azure Blob Storage

## Application-Level Configuration

In addition to nginx configuration, the ASP.NET Core application is also configured to handle large uploads:

### Program.cs
- **Kestrel request body size limit**: 50 MB (52,428,800 bytes)
- **Form options multipart body limit**: 50 MB
- **Form options value length limit**: 50 MB

### FilesController.cs
- **RequestSizeLimit attribute**: 50 MB on the upload endpoint

## Deployment

These configuration files are automatically deployed with your Elastic Beanstalk application:

1. EB copies `.platform/` directory contents to the EC2 instance
2. Nginx configuration files in `.platform/nginx/conf.d/` are automatically included
3. Nginx restarts with the new configuration

**No manual nginx configuration needed!**

## Testing

After deployment, test with a file upload:

```bash
# Test with a 10MB file
curl -X POST \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -F "file=@large-file.pdf" \
  -F "moduleId=1" \
  -F "name=Test Upload" \
  https://api.dev.tutoria.tec.br/api/files/
```

## Limits

| Layer | Limit | Purpose |
|-------|-------|---------|
| **Nginx** | 50 MB | Reverse proxy entry point |
| **Kestrel** | 50 MB | ASP.NET Core web server |
| **Form Options** | 50 MB | Multipart form handling |
| **Controller** | 50 MB | Explicit endpoint limit |
| **Azure Blob Storage** | ~195 GB | Storage backend (Block Blob limit) |

## Troubleshooting

### Error: "client intended to send too large body"
- **Cause**: Nginx is rejecting the upload before it reaches the application
- **Solution**: Verify `.platform/nginx/conf.d/client_max_body_size.conf` is deployed
- **Check**: SSH into EB instance and verify `/etc/nginx/conf.d/elasticbeanstalk/` contains the config

### Error: "Request body too large"
- **Cause**: ASP.NET Core Kestrel is rejecting the upload
- **Solution**: Verify `Program.cs` has Kestrel configuration with 50 MB limit
- **Check**: Review application logs in CloudWatch

### Timeout during upload
- **Cause**: Upload takes longer than proxy timeout (default 60s)
- **Solution**: Verify `.platform/nginx/conf.d/proxy_settings.conf` is deployed with 300s timeouts
- **Check**: Monitor network speed and file size ratio

## References

- [AWS EB Platform Hooks](https://docs.aws.amazon.com/elasticbeanstalk/latest/dg/platforms-linux-extend.html)
- [Nginx client_max_body_size](http://nginx.org/en/docs/http/ngx_http_core_module.html#client_max_body_size)
- [ASP.NET Core Kestrel Limits](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/options)
