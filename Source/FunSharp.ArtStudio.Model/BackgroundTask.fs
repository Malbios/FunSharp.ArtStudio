namespace FunSharp.ArtStudio.Model

type BackgroundTask =
    | Inspiration of url: string
    | ChatGPT of ChatGPTTask
    | Sora of SoraTask
