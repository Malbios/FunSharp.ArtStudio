namespace DeviantArt.Api

type IPersistence<'T> =
    abstract member Load : unit -> 'T option
    abstract member Save : 'T -> unit
