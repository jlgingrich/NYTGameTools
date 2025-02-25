namespace NYTGT

open System
open FSharp.Data
open Thoth.Json.Net

/// Extensions to <code>Thoth.Json.Net.Decode</code>.
module Decode =
    let exactlyOne (decoder: Decoder<'t>) : Decoder<'t> =
        Decode.list decoder
        |> Decode.andThen (function
            | [ item ] -> Decode.succeed item
            | items -> Decode.fail $"Expected exactly one list entry, but got {List.length items} entries")

/// Extensions to <code>Microsoft.FSharp.Core.Result</code>.
module Result =
    let assertOk =
        function
        | Ok x -> x
        | Error e -> failwithf "Assertion failed: %A" e

module Helpers =
    let version i = $"v%d{i}"

    [<Literal>]
    let DateFormat = "yyyy-MM-dd"

    let formatDate (date: DateTime) = date.ToString(DateFormat)

    let urlForDate game v (date: DateTime) =
        $"https://www.nytimes.com/svc/%s{game}/%s{version v}/%s{formatDate date}.json"

    let getRequest url =
        Http.RequestString url |> String.filter (Char.IsAscii)

/// Common information that is published with each NYT game
type PublicationInformation = {
    Id: int
    PrintDate: DateTime
    Editor: string option
    Constructors: string list
}

// API implementations for each game

module Strands =
    type Game = {
        Info: PublicationInformation
        Clue: string
        Spangram: string
        ThemeWords: string list
        Board: string array
    }


    let getRaw date =
        Helpers.urlForDate "strands" 2u date |> Helpers.getRequest

    let private decoder: Decoder<Game> =
        Decode.object (fun get -> {
            Info = {
                Id = get.Required.Field "id" Decode.int
                PrintDate = get.Required.Field "printDate" Decode.datetimeLocal
                Editor = get.Optional.Field "editor" Decode.string
                Constructors = get.Required.Field "constructors" Decode.string |> List.singleton
            }
            Clue = get.Required.Field "clue" Decode.string
            Spangram = get.Required.Field "spangram" Decode.string
            ThemeWords = get.Required.Field "themeWords" (Decode.list Decode.string)
            Board = get.Required.Field "startingBoard" (Decode.array Decode.string)
        })


    let parse = Decode.fromString decoder

    let getGame date = getRaw date |> parse

    let getCurrentGame () = DateTime.Now |> getGame

module Connections =
    type Category = { Title: string; Cards: string list }

    type Game = {
        Info: PublicationInformation
        Categories: Category list
    }


    let getRaw date =
        Helpers.urlForDate "connections" 2u date |> Helpers.getRequest

    let private decodeCategory: Decoder<Category> =
        Decode.object (fun get -> {
            Title = get.Required.Field "title" Decode.string
            Cards =
                get.Required.Field
                    "cards"
                    (Decode.list (Decode.object (fun get -> get.Required.Field "content" Decode.string)))
        })

    let private decoder: Decoder<Game> =
        Decode.object (fun get -> {
            Info = {
                Id = get.Required.Field "id" Decode.int
                PrintDate = get.Required.Field "print_date" Decode.datetimeLocal
                Editor = get.Optional.Field "editor" Decode.string
                Constructors = List.empty
            }
            Categories = get.Required.Field "categories" (Decode.list decodeCategory)
        })

    let parse = Decode.fromString decoder

    let getGame date = getRaw date |> parse

    let getCurrentGame () = DateTime.Now |> getGame

module ConnectionsSportsEdition =
    type Category = { Title: string; Cards: string list }

    type Game = {
        Info: PublicationInformation
        Categories: Category list
    }

    let getRaw date =
        $"https://www.nytimes.com/games-assets/sports-connections/{Helpers.formatDate date}.json"
        |> Helpers.getRequest

    let private decodeCategory: Decoder<Category> =
        Decode.object (fun get -> {
            Title = get.Required.Field "title" Decode.string
            Cards =
                get.Required.Field
                    "cards"
                    (Decode.list (Decode.object (fun get -> get.Required.Field "content" Decode.string)))
        })

    let private decoder: Decoder<Game> =
        Decode.object (fun get -> {
            Info = {
                Id = get.Required.Field "id" Decode.int
                PrintDate = get.Required.Field "printDate" Decode.datetimeLocal
                Editor = get.Optional.Field "editor" Decode.string
                Constructors = List.empty
            }
            Categories = get.Required.Field "categories" (Decode.list decodeCategory)
        })

    let parse = Decode.fromString decoder

    let getGame date = getRaw date |> parse

    let getCurrentGame () = DateTime.Now |> getGame

module LetterBoxed =
    type Game = {
        Info: PublicationInformation
        Sides: string list
        Solution: string list
        Par: int
    }


    let getRaw () =
        let scriptPrefix = "window.gameData = "

        HtmlDocument.Load "https://www.nytimes.com/puzzles/letter-boxed"
        |> fun n -> n.CssSelect "script[type=text/javascript]"
        |> List.filter (fun n -> n.DirectInnerText().StartsWith scriptPrefix)
        |> List.exactlyOne
        |> fun n -> n.DirectInnerText()[String.length scriptPrefix ..]


    let private decoder: Decoder<Game> =
        Decode.object (fun get -> {
            Info = {
                Id = get.Required.Field "id" Decode.int
                PrintDate = get.Required.Field "printDate" Decode.datetimeLocal
                Editor = get.Optional.Field "editor" Decode.string
                Constructors = List.empty
            }
            Sides = get.Required.Field "sides" (Decode.list Decode.string)
            Solution = get.Required.Field "ourSolution" (Decode.list Decode.string)
            Par = get.Required.Field "par" Decode.int
        })

    let parse = Decode.fromString decoder

    let getCurrentGame () = getRaw () |> parse

module SpellingBee =
    type Game = {
        Info: PublicationInformation
        CenterLetter: char
        OuterLetters: char list
        Answers: string list
    }


    let getRaw () =
        let scriptPrefix = "window.gameData = "

        HtmlDocument.Load "https://www.nytimes.com/puzzles/spelling-bee"
        |> fun n -> n.CssSelect "script[type=text/javascript]"
        |> List.filter (fun n -> n.DirectInnerText().StartsWith scriptPrefix)
        |> List.exactlyOne
        |> fun n -> n.DirectInnerText()[String.length scriptPrefix ..]


    let private decoder: Decoder<Game> =
        Decode.object (fun get -> {
            Info = {
                Id = get.Required.At [ "today"; "id" ] Decode.int
                PrintDate = get.Required.At [ "today"; "printDate" ] Decode.datetimeLocal
                Editor = get.Optional.At [ "today"; "editor" ] Decode.string
                Constructors = List.empty
            }
            CenterLetter = get.Required.At [ "today"; "centerLetter" ] Decode.char
            OuterLetters = get.Required.At [ "today"; "outerLetters" ] (Decode.list Decode.char)
            Answers = get.Required.At [ "today"; "answers" ] (Decode.list Decode.string)
        })

    let parse = Decode.fromString decoder

    let getCurrentGame () = getRaw () |> parse

module Wordle =
    type Game = {
        Info: PublicationInformation
        Solution: string
    }


    let getRaw date =
        Helpers.urlForDate "wordle" 2u date |> Helpers.getRequest

    let private decoder: Decoder<Game> =
        Decode.object (fun get -> {
            Info = {
                Id = get.Required.Field "id" Decode.int
                PrintDate = get.Required.Field "print_date" Decode.datetimeLocal
                Editor = get.Optional.Field "editor" Decode.string
                Constructors = List.empty
            }
            Solution = get.Required.Field "solution" Decode.string |> _.ToUpper()
        })

    let parse = Decode.fromString decoder

    let getGame date = getRaw date |> parse

    let getCurrentGame () = DateTime.Now |> getGame

module Mini =
    type Direction =
        | Across
        | Down

    type Clue = {
        Direction: Direction
        Label: int
        Hint: string
    }

    type Game = {
        Info: PublicationInformation
        Solution: char option array array
        Clues: Clue list
        Height: int
        Width: int
    }

    let getRaw () =
        "https://www.nytimes.com/svc/crosswords/v6/puzzle/mini.json"
        |> Helpers.getRequest

    let private decodeDirection: Decoder<Direction> =
        Decode.string
        |> Decode.andThen (function
            | "Across" -> Decode.succeed Across
            | "Down" -> Decode.succeed Down
            | invalid -> Decode.fail (sprintf " `%s` is an invalid clue direction" invalid))

    let private decodeClue: Decoder<Clue> =
        Decode.object (fun get -> {
            Direction = get.Required.Field "direction" decodeDirection
            Label = get.Required.Field "label" Decode.int
            Hint =
                get.Required.Field
                    "text"
                    (Decode.exactlyOne (Decode.object (fun get -> get.Required.Field "plain" Decode.string)))
        })

    let private decodeCell: Decoder<char option> =
        Decode.object (fun get -> get.Optional.Field "answer" Decode.string)
        |> Decode.andThen (Option.map Char.Parse >> Decode.succeed)

    let private decoder: Decoder<Game> =
        Decode.object (fun get ->
            let boardHeight =
                get.Required.Field
                    "body"
                    (Decode.exactlyOne (
                        Decode.object (fun get -> get.Required.At [ "dimensions"; "height" ] Decode.int)
                    ))

            let boardWidth =
                get.Required.Field
                    "body"
                    (Decode.exactlyOne (
                        Decode.object (fun get -> get.Required.At [ "dimensions"; "width" ] Decode.int)
                    ))

            {
                Info = {
                    Id = get.Required.Field "id" Decode.int
                    PrintDate = get.Required.Field "publicationDate" Decode.datetimeLocal
                    Editor = get.Optional.Field "editor" Decode.string
                    Constructors = get.Required.Field "constructors" (Decode.list Decode.string)
                }
                Solution =
                    get.Required.Field
                        "body"
                        (Decode.exactlyOne (
                            Decode.object (fun get -> get.Required.Field "cells" (Decode.array decodeCell))
                         )
                         |> Decode.map (Array.chunkBySize boardWidth))
                Clues =
                    get.Required.Field
                        "body"
                        (Decode.exactlyOne (
                            Decode.object (fun get -> get.Required.Field "clues" (Decode.list decodeClue))
                        ))
                Height = boardHeight
                Width = boardWidth
            })

    let parse = Decode.fromString decoder

    let getCurrentGame () = getRaw () |> parse


module Crossword =
    type Direction =
        | Across
        | Down

    type Clue = {
        Direction: Direction
        Label: int
        Hint: string
    }

    type Game = {
        Info: PublicationInformation
        Solution: string option array array
        Clues: Clue list
        Height: int
        Width: int
    }

    let getRaw () =
        Http.RequestString(
            "https://www.nytimes.com/svc/crosswords/v6/puzzle/daily.json",
            headers = [ "x-games-auth-bypass", "true" ]
        )
        |> String.filter (Char.IsAscii)

    let private decodeDirection: Decoder<Direction> =
        Decode.string
        |> Decode.andThen (function
            | "Across" -> Decode.succeed Across
            | "Down" -> Decode.succeed Down
            | invalid -> Decode.fail (sprintf " `%s` is an invalid clue direction" invalid))

    let private decodeClue: Decoder<Clue> =
        Decode.object (fun get -> {
            Direction = get.Required.Field "direction" decodeDirection
            Label = get.Required.Field "label" Decode.int
            Hint =
                get.Required.Field
                    "text"
                    (Decode.exactlyOne (Decode.object (fun get -> get.Required.Field "plain" Decode.string)))
        })

    let private decodeCell: Decoder<string option> =
        Decode.object (fun get -> get.Optional.Field "answer" Decode.string)

    let private decoder: Decoder<Game> =
        Decode.object (fun get ->
            let boardHeight =
                get.Required.Field
                    "body"
                    (Decode.exactlyOne (
                        Decode.object (fun get -> get.Required.At [ "dimensions"; "height" ] Decode.int)
                    ))

            let boardWidth =
                get.Required.Field
                    "body"
                    (Decode.exactlyOne (
                        Decode.object (fun get -> get.Required.At [ "dimensions"; "width" ] Decode.int)
                    ))

            {
                Info = {
                    Id = get.Required.Field "id" Decode.int
                    PrintDate = get.Required.Field "publicationDate" Decode.datetimeLocal
                    Editor = get.Optional.Field "editor" Decode.string
                    Constructors = get.Required.Field "constructors" (Decode.list Decode.string)
                }
                Solution =
                    get.Required.Field
                        "body"
                        (Decode.exactlyOne (
                            Decode.object (fun get -> get.Required.Field "cells" (Decode.array decodeCell))
                         )
                         |> Decode.map (Array.chunkBySize boardWidth))
                Clues =
                    get.Required.Field
                        "body"
                        (Decode.exactlyOne (
                            Decode.object (fun get -> get.Required.Field "clues" (Decode.list decodeClue))
                        ))
                Height = boardHeight
                Width = boardWidth
            })

    let parse = Decode.fromString decoder

    let getCurrentGame () = getRaw () |> parse

module Suduko =
    [<RequireQualifiedAccess>]
    type Difficulty =
        | Easy
        | Medium
        | Hard

    let parseDifficulty s =
        match s with
        | "easy" -> Difficulty.Easy
        | "medium" -> Difficulty.Medium
        | "hard" -> Difficulty.Hard
        | e -> failwithf "Unknown difficulty: '%s'" e

    type Game = {
        Info: PublicationInformation
        Puzzle: (int option) array array
        Solution: int array array
    }

    let getRaw () =
        let scriptPrefix = "window.gameData = "

        HtmlDocument.Load "https://www.nytimes.com/puzzles/sudoku"
        |> fun n -> n.CssSelect "script[type=text/javascript]"
        |> List.filter (fun n -> n.DirectInnerText().StartsWith scriptPrefix)
        |> List.exactlyOne
        |> fun n -> n.DirectInnerText()[String.length scriptPrefix ..]

    let private decoder: Decoder<Game> =
        Decode.object (fun get -> {
            Info = {
                Id = get.Required.Field "puzzle_id" Decode.int
                PrintDate = get.Required.Field "published" Decode.datetimeLocal
                Editor = None
                Constructors = List.empty
            }
            Puzzle =
                get.Required.At
                    [ "puzzle_data"; "puzzle" ]
                    (Decode.array (Decode.int |> Decode.map (fun i -> if i = 0 then None else Some i))
                     |> Decode.map (Array.chunkBySize 9))
            Solution =
                get.Required.At
                    [ "puzzle_data"; "solution" ]
                    (Decode.array Decode.int |> Decode.map (Array.chunkBySize 9))
        })

    let private decodeData: Decoder<Map<Difficulty, Game>> =
        CustomDecoders.keyValueOptions (CustomDecoders.ignoreFail decoder)
        |> Decode.andThen (fun kvs -> kvs |> List.map (fun (k, v) -> (parseDifficulty k), v) |> Decode.succeed)
        |> Decode.map Map

    let parse = Decode.fromString decodeData

    let getCurrentGame () = getRaw () |> parse
