namespace FunSharp.ArtStudio.Model

type Paragraph =
    | First
    | Last
    | Index of index: int

type SnippetAction =
    | Append of Paragraph
    | Replace of Paragraph

type ClipboardSnippet = {
    label: string
    value: string
    action: SnippetAction
}
