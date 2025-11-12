$chromeProfiles = 'C:/dev/fsharp/FunSharp.ArtStudio/Utilities/puppeteer/ChromeProfiles'
$manualChromeProfiles = 'C:/dev/fsharp/FunSharp.ArtStudio/Utilities/puppeteer/ManualChromeProfiles'

# $chromeProfilesPath = $chromeProfiles
$chromeProfilesPath = $manualChromeProfiles

& "C:/Program Files/Google/Chrome/Application/chrome.exe" --user-data-dir=$chromeProfilesPath --profile-dir='Profile 2'