(**
---
categoryindex: 0
index: 1
title: UrlTemplates
category: Libraries
description: A library for creating and parsing URL templates.
keywords: url, template, url-parsing, validation
---

This library specializes itself in parsing URL-like strings into structured objects. It is used by [Navs](./Navs.fsx) to parse navigable URLs and URL templates to find if they match.
*)

(*** hide ***)
#r "nuget: UrlTemplates, 1.0.0-beta-008"

open System
(**
## Usage

For most use cases, you will want to use the existing `RouteMatcher` module. This module provides a single function `matchStrings` that takes a URL template and a URL and returns a `Result` type with the parsed URL template, the parsed URL, and the matched parameters.
*)

open UrlTemplates.RouteMatcher

let template = "/api/v1/profiles/:id<guid>?activity<bool>#hash"

let url = $"/api/v1/profiles/{Guid.NewGuid()}?activity=false#profile"

let urlTemplate, urlInfo, urlMatch =
  match RouteMatcher.matchStrings template url with
  | Ok(urlTemplate, urlInfo, urlMatch) -> urlTemplate, urlInfo, urlMatch
  | Error errors -> failwithf "Failed to match URL: %A" errors

(**
The `urlTemplate` is the parsed URL template, the `urlInfo` is the parsed URL, and the `urlMatch` is the matched parameters.

For example the contents of the URL template can be inspected and this information is what is ultimately used to determine if a URL matches a templated string.
*)

(*** hide ***)
printfn "\n\n%A\n\n" urlTemplate

(*** include-output ***)

(**
 For the above case it means that URLs will be matched as long as they contain the `/api/v1/profiles/` prefix, followed by a GUID, and an optional query parameter `activity` that is a boolean. The URL can also contain a hash.
*)


(**
  For the case of the `urlInfo` it contains the parsed URL as a plain string, it doesn't contain any information about the matched parameters.
*)

(*** hide ***)
printfn "\n\n%A\n\n" urlInfo
(*** include-output ***)

(**
  Finally, the `urlMatch` contains the matched parameters if they were supplied in the URL.
  For the above case, it will contain the matched GUID and the matched boolean.
*)

(*** hide ***)
printfn
  "Segments and Params: %s\n"
  (urlMatch.Params
   |> Seq.map(fun (KeyValue(k, v)) -> $"%A{v}")
   |> String.concat "\n - ")

printfn
  "Query Params: %s\n"
  (urlMatch.QueryParams
   |> Seq.map(fun (KeyValue(k, v)) -> $"{k}=%A{v}")
   |> String.concat "\n - ")

printfn "Hash: %A" urlMatch.Hash
(*** include-output ***)


(**
## Providing Types for Parameters

Given that parameters can be matched against a type, it is possible to provide some of the Well known types included in `cref:T:UrlTemplates.UrlTemplate.TypedParam`
The general syntax is `paramName<type>` where `type` is one of the following:

- `int`
- `float`
- `bool`
- `guid`
- `long`
- `decimal`

For parameters where the name is not specified, the automatic type will be `string`.

Names must be alphanumeric strings and may contain `-` and `_` characters.

### Segment params

To specify a type for a segment parameter, use the following syntax:

- `/:paramName<type>`

For example:

- `/api/v1/users/:id<guid>/items/:itemId<int>`

Please note that every segment parameter is required even the trailing one.

### Query params

To specify a type for a query parameter, use the following syntax:

- `?paramName<type>` - for optional query parameters
- `?paramName<type>!` - for required query parameters

For example:

- `/?name!&age<int>`

In this case, the `name` query parameter is required and must be a string, and the `age` query parameter is optional but if suipplied it must be an integer.

### Hash

The hash is a ValueOption string and it is specified as follows:

- `#hash`

For example:

- `/docs/functions#callbacks`


## Extracting Parameters from a Matched URL

The `UrlMatch` type provides a set of functions to extract parameters from a matched URL.

*)


(*** hide ***)

open UrlTemplates.RouteMatcher

(*** show ***)

let tplDefinition = "/api?name&age<int>&statuses"
let targetUrl = "/api?name=john&age=30&statuses=active&statuses=inactive"

let _, _, matchedUrl =
  RouteMatcher.matchStrings tplDefinition targetUrl
  |> Result.defaultWith(fun e -> failwithf "Expected Ok, got Error %O" e)


(**

Normally query parameters are stored in string, object read only dictionaries however we offer a few utilities to extract them into a more useful format.
for de F# folks you can use the following functions:
*)

let name = matchedUrl |> UrlMatch.getFromParams<string> "name"

let age = matchedUrl |> UrlMatch.getFromParams<int> "age"

let values = matchedUrl |> UrlMatch.getParamSeqFromQuery<string> "statuses"

let statuses =
  values |> ValueOption.defaultWith(fun _ -> failwith "Expected a value")

(*** hide ***)

printfn $"%A{statuses}"
(*** include-output ***)


(**
For the non F# folks extension methods are also provided

```csharp
var name = urlMatch.GetFromParams<string>("name");

var age = urlMatch.GetFromParams<int>("age");

var values = urlMatch.GetParamSeqFromQuery<string>("statuses");

if (values.IsNone)
{
  throw new Exception("Expected a value");
}

Console.WriteLine($"{values.Value[0]}, {values.Value[1]}");
// inactive, active
```

> ***Note:*** The ``cref:M:UrlTemplates.RouteMatcher.UrlMatchModule.getParamSeqFromQuery`1`` function  and its extension method counterpart
> does not guarantee that the values are in the same order as they were in the URL.

*)
