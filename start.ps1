$preprocessorPath = "$PSScriptRoot/reveal-preprocessor.js"
# Convert to file URI for reveal-md compatibility
$preprocessorUri = [Uri]$preprocessorPath

Write-Host "Starting presentation with preprocessor..."
reveal-md slides.md --preprocessor $preprocessorUri.AbsoluteUri --port 8080 --watch
