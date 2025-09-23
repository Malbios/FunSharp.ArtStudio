namespace FunSharp.DeviantArt.Api

[<RequireQualifiedAccess>]
module Endpoints =
    
    [<Literal>]
    let private common = "https://www.deviantart.com/api/v1/oauth2"
    
    let submitToStash =
        $"{common}/stash/submit"
        
    let publishFromStash =
        $"{common}/stash/publish"
    
    let whoAmI =
        $"{common}/user/whoami"
    
    let allDeviations limit offset =
        $"{common}/gallery/all?with_session=false&mature_content=true&limit={limit}&offset={offset}"
        
    let deviationMetadata query =
        $"{common}/deviation/metadata?{query}&ext_stats=true"
        
    let galleryFolders (limit: int) =
        $"{common}/gallery/folders?limit={limit}"
        
    let deviation id =
        $"{common}/deviation/{id}"
