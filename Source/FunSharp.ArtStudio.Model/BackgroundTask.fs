namespace FunSharp.ArtStudio.Model

type BackgroundTask =
    | Sora of SoraTask
    | Inspiration of url: string
