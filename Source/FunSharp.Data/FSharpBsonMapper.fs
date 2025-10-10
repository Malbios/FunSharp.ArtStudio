namespace FunSharp.Data

open System
open System.Linq.Expressions
open System.Collections.Concurrent
open Microsoft.FSharp.Reflection
open LiteDB

module private Helpers =
    let buildIdExpr<'T> (pi: Reflection.PropertyInfo) =
        let param = Expression.Parameter(typeof<'T>, "x")
        let body = Expression.Convert(Expression.Property(param, pi), typeof<obj>)
        Expression.Lambda<Func<'T, obj>>(body, param)

type FSharpBsonMapper() as this =
    inherit BsonMapper()

    let unionCache = ConcurrentDictionary<string, UnionCaseInfo[]>()
    let typeCache = ConcurrentDictionary<string, Type>()
    let registered = ConcurrentDictionary<Type, unit>()
    
    let serialize o =
        if o = null then
            BsonValue.Null
        else
            let t = o.GetType()
            if FSharpType.IsUnion t then
                let case, fields = FSharpValue.GetUnionFields(o, t)
                let doc = BsonDocument()
                doc["__case"] <- case.Name
                doc["__type"] <- t.AssemblyQualifiedName
                doc["fields"] <- BsonArray(fields |> Array.map this.Serialize)
                BsonValue(doc)
            elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Option<_>> then
                let case, fields = FSharpValue.GetUnionFields(o, t)
                match case.Name, fields with
                | "None", _ -> BsonValue.Null
                | "Some", [|v|] -> this.Serialize(v)
                | _ -> failwith "Invalid Option"
            else
                this.Serialize(o)
                
    let deserialize (bson: BsonValue) =
        if not bson.IsDocument then
            bson :> obj
        else
            let doc = bson.AsDocument
            if doc.ContainsKey("__case") && doc.ContainsKey("__type") then
                let typeName = doc["__type"].AsString
                let caseName = doc["__case"].AsString
                let unionType = typeCache.GetOrAdd(typeName, fun n -> Type.GetType(n, true))
                let cases = unionCache.GetOrAdd(typeName, fun _ -> FSharpType.GetUnionCases(unionType))
                let case = cases |> Array.find (fun c -> c.Name = caseName)
                let fields =
                    match doc["fields"] with
                    | null -> [||]
                    | a when a.IsArray ->
                        a.AsArray
                        |> Seq.toArray
                        |> Array.zip (case.GetFields())
                        |> Array.map (fun (pi, bv) -> this.Deserialize(pi.PropertyType, bv))
                    | _ -> [||]
                FSharpValue.MakeUnion(case, fields)
            else bson :> obj

    do
        this.IncludeFields <- false
        this.EnumAsInteger <- false

        this.RegisterType<obj>(serialize, deserialize)

    member this.EnsureRecord<'T>() =
        let t = typeof<'T>
        if registered.TryAdd(t, ()) && FSharpType.IsRecord t then
            let eb = this.Entity<'T>()
            for pi in FSharpType.GetRecordFields t do
                if pi.Name = "Id" || pi.Name = "id" then
                    let expr = Helpers.buildIdExpr<'T>(pi)
                    eb.Id(expr) |> ignore
        this
