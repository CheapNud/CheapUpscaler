# Tag-driven release: self-contained win-x64 publish, zip + SHA256, Forgejo release with assets.
# Inputs come from the environment (set by .forgejo/workflows/release.yml):
#   REF_NAME    tag name, e.g. v1.2.3
#   FORGE_TOKEN Actions token with release rights on the repo
#   REPO        owner/name
#   SERVER_URL  forge base URL
$ErrorActionPreference = "Stop"

$tag = $env:REF_NAME
$version = $tag.TrimStart('v')
$zip = "CheapUpscaler-v$version-win-x64.zip"

# No PublishSingleFile: Blazor static web assets and the Photino native
# library do not survive single-file extraction reliably
dotnet publish CheapUpscaler.Blazor/CheapUpscaler.Blazor.csproj --configuration Release --runtime win-x64 --self-contained true --output publish/desktop /p:Version=$version
if ($LASTEXITCODE -ne 0) { exit 1 }

Compress-Archive -Path publish/desktop/* -DestinationPath $zip
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
"$hash  $zip" | Out-File -FilePath checksums.txt -Encoding ascii

$apiBase = "$env:SERVER_URL/api/v1/repos/$env:REPO"
$headers = @{ Authorization = "token $env:FORGE_TOKEN" }
$releaseBody = @{
  tag_name = $tag
  name = "CheapUpscaler $version"
  body = "Self-contained win-x64 build, no .NET runtime required. Extract and run CheapUpscaler.Blazor.exe. AI processing needs an NVIDIA GPU plus VapourSynth/ffmpeg tooling on the machine. Verify downloads against checksums.txt (SHA256)."
} | ConvertTo-Json
$release = Invoke-RestMethod -Method Post -Uri "$apiBase/releases" -Headers $headers -ContentType "application/json" -Body $releaseBody
foreach ($asset in @($zip, "checksums.txt")) {
  # curl.exe: Windows PowerShell 5.1 has no -Form for multipart uploads
  curl.exe -sf -X POST -H "Authorization: token $env:FORGE_TOKEN" -F "attachment=@$asset" "$apiBase/releases/$($release.id)/assets?name=$asset"
  if ($LASTEXITCODE -ne 0) { exit 1 }
}
