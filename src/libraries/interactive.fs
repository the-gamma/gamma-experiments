﻿namespace TheGamma.Interactive

open Fable.Helpers
open Fable.Import.Browser

open TheGamma.Common
open TheGamma.Series
open TheGamma.Html
open TheGamma.Interactive.Compost

// ------------------------------------------------------------------------------------------------
// You Draw
// ------------------------------------------------------------------------------------------------

module CompostHelpers = 
  let (|Cont|) = function COV(CO x) -> x | _ -> failwith "Expected continuous value"
  let (|Cat|) = function CAR(CA x, r) -> x, r | _ -> failwith "Expected categorical value"
  let Cont x = COV(CO x)
  let Cat(x, r) = CAR(CA x, r)

open CompostHelpers

module YouDrawHelpers = 
  type YouDrawEvent = 
    | ShowResults
    | Draw of float * float

  type YouDrawState = 
    { Completed : bool
      Clip : float
      Data : (float * float)[]
      Guessed : (float * option<float>)[] }

  let initState data clipx = 
    { Completed = false
      Data = data
      Clip = clipx
      Guessed = [| for x, y in data do if x > clipx then yield x, None |] }

  let handler state evt = 
    match evt with
    | ShowResults -> { state with Completed = true }
    | Draw (downX, downY) ->
        let indexed = Array.indexed state.Guessed
        let nearest, _ = indexed |> Array.minBy (fun (_, (x, _)) -> abs (downX - x))
        { state with
            Guessed = indexed |> Array.map (fun (i, (x, y)) -> 
              if i = nearest then (x, Some downY) else (x, y)) }

  let render (width, height) (topLbl, leftLbl, rightLbl) (leftClr,rightClr,guessClr) (loy, hiy) trigger state = 
    let all = 
      [| for x, y in state.Data -> Cont x, Cont y |]
    let known = 
      [| for x, y in state.Data do if x <= state.Clip then yield Cont x, Cont y |]
    let right = 
      [| yield Array.last known
         for x, y in state.Data do if x > state.Clip then yield Cont x, Cont y |]
    let guessed = 
      [| yield Array.last known
         for x, y in state.Guessed do if y.IsSome then yield Cont x, Cont y.Value |]

    let lx, ly = (fst (Seq.head state.Data) + float state.Clip) / 2., loy + (hiy - loy) / 10.
    let rx, ry = (fst (Seq.last state.Data) + float state.Clip) / 2., loy + (hiy - loy) / 10.
    let tx, ty = float state.Clip, hiy - (hiy - loy) / 10.
    let setColor c s = { s with Font = "12pt sans-serif"; Fill=Solid(1.0, HTML c); StrokeColor=(0.0, RGB(0,0,0)) }
    let labels = 
      Shape.Layered [
        Style(setColor leftClr, Shape.Text(COV(CO lx), COV(CO ly), VerticalAlign.Baseline, HorizontalAlign.Center, leftLbl))
        Style(setColor rightClr, Shape.Text(COV(CO rx), COV(CO ry), VerticalAlign.Baseline, HorizontalAlign.Center, rightLbl))
        Style(setColor guessClr, Shape.Text(COV(CO tx), COV(CO ty), VerticalAlign.Baseline, HorizontalAlign.Center, topLbl))
      ]

    let chart = 
      Axes(true, true,
        Interactive(
          ( if state.Completed then []
            else
              [ MouseMove(fun evt (Cont x, Cont y) -> 
                  if (int evt.buttons) &&& 1 = 1 then trigger(Draw(x, y)) )
                TouchMove(fun evt (Cont x, Cont y) -> 
                  trigger(Draw(x, y)) )
                MouseDown(fun evt (Cont x, Cont y) -> trigger(Draw(x, y)) )
                TouchStart(fun evt (Cont x, Cont y) -> trigger(Draw(x, y)) ) ]),
          Shape.Scale
            ( None, Some(CO loy, CO hiy), 
              Layered [
                yield labels
                yield Style(Drawing.hideFill >> Drawing.hideStroke, Line all)
                yield Style(
                  (fun s -> { s with StrokeColor = (1.0, HTML leftClr); Fill = Solid(0.2, HTML leftClr) }), 
                  Layered [ Area known; Line known ]) 
                if state.Completed then
                  yield Style((fun s -> 
                    { s with 
                        StrokeColor = (1.0, HTML rightClr)
                        StrokeDashArray = [ Percentage 0.; Percentage 100. ]
                        Fill = Solid(0.0, HTML rightClr)
                        Animation = Some(1000, "ease", fun s -> 
                          { s with
                              StrokeDashArray = [ Percentage 100.; Percentage 0. ]
                              Fill = Solid(0.2, HTML rightClr) } 
                        ) }), 
                    Layered [ Area right; Line right ])                 
                if guessed.Length > 1 then
                  yield Style(
                    (fun s -> { s with StrokeColor = (1.0, HTML guessClr); StrokeDashArray = [ Integer 5; Integer 5 ] }), 
                    Line guessed ) 
              ])
        )) 
    let chart = Scale(Some(CO (fst (Seq.head state.Data)), CO (fst (Seq.last state.Data))), None, chart)
    
    h?div ["style"=>"text-align:center;padding-top:20px"] [
      Compost.createSvg (width, height) chart
      h?div ["style"=>"padding-bottom:20px"] [
        h?button [
            yield "type" => "button"
            yield "click" =!> fun _ _ -> trigger ShowResults
            if state.Guessed |> Seq.last |> snd = None then
              yield "disabled" => "disabled"
          ] [ text "Show me how I did" ]
        ]
    ]


type youdraw = 
  { data : series<float, float> 
    clip : float option
    min : float option
    max : float option 
    knownColor : string option
    unknownColor : string option 
    drawColor : string option 
    topLabel : string option
    knownLabel : string option
    guessLabel : string option }
  static member create(data:series<float, float>) =
    { youdraw.data = data
      clip = None; min = None; max = None 
      guessLabel = None; topLabel = None; knownLabel = None;
      knownColor = None; unknownColor = None; drawColor = None }
  static member createAny<'a>(data:series<'a, float>) =
    { youdraw.data = data.mapKeys(unbox)
      clip = None; min = None; max = None 
      guessLabel = None; topLabel = None; knownLabel = None;
      knownColor = None; unknownColor = None; drawColor = None }
  member y.setRange(min, max) = { y with min = Some min; max = Some max }
  member y.setClip(clip) = { y with clip = Some clip }
  member y.setColors(known, unknown) = { y with knownColor = Some known; unknownColor = Some unknown }
  member y.setDrawColor(draw) = { y with drawColor = Some draw }
  member y.setLabels(top, known, guess) = { y with knownLabel = Some known; topLabel = Some top; guessLabel = Some guess }
  member y.show(outputId) =   
    async { 
      let id = "container" + System.Guid.NewGuid().ToString().Replace("-", "")
      h?div ["id" => id] [ ] |> renderTo (document.getElementById(outputId))        

      // Get data & wait until the element is created
      let! data = y.data.data |> Async.AwaitFuture 
      let mutable i = 10
      while i > 0 && document.getElementById(id) = null do
        do! Async.Sleep(10)
        i <- i - 1
      let element = document.getElementById(id)
      let size = element.clientWidth, max 400. (element.clientWidth / 2.) 

      try
        let loy = match y.min with Some v -> v | _ -> data |> Seq.map snd |> Seq.min
        let hiy = match y.max with Some v -> v | _ -> data |> Seq.map snd |> Seq.max
        let clipx = match y.clip with Some v -> v | _ -> fst (data.[data.Length / 2])
        let data = Array.sortBy fst data
        let lc, dc, gc = defaultArg y.knownColor "#606060", defaultArg y.unknownColor "#FFC700", defaultArg y.drawColor "#808080"          
        Compost.app outputId 
          (YouDrawHelpers.initState data clipx) 
          (YouDrawHelpers.render size
            (defaultArg y.topLabel "", defaultArg y.knownLabel "", defaultArg y.guessLabel "") 
            (lc,dc,gc) (loy, hiy)) YouDrawHelpers.handler
      with e ->
        Log.exn("GUI", "Interactive rendering failed: %O", e)
    } |> Async.StartImmediate  

// ------------------------------------------------------------------------------------------------
// You Guess Bar
// ------------------------------------------------------------------------------------------------

module YouGuessColsHelpers = 

  type YouGuessState = 
    { Completed : bool
      CompletionStep : float
      Default : float
      Maximum : float
      Data : (string * float)[]
      Guesses : Map<string, float> }

  type YouGuessEvent = 
    | ShowResults 
    | Animate 
    | Update of string * float

  let initState data maxValue =     
    let max = match maxValue with Some m -> m | _ -> Seq.max (Seq.map snd data)
    let max = 
      match Scales.generateContinuousRange (CO 0.0) (CO max) with
      | Scales.Continuous(_, CO max), _, _ -> max
      | _ -> failwith "Failed to calculate maximum"
    { Completed = false
      CompletionStep = 0.0
      Data = data 
      Default = Array.averageBy snd data
      Maximum = max
      Guesses = Map.empty }

  let update state evt = 
    match evt with
    | ShowResults -> { state with Completed = true }
    | Animate -> { state with CompletionStep = min 1.0 (state.CompletionStep + 0.05) }
    | Update(k, v) -> { state with Guesses = Map.add k v state.Guesses }

  let vega10 = ["#1f77b4"; "#ff7f0e"; "#2ca02c"; "#d62728"; "#9467bd"; "#8c564b"; "#e377c2"; "#7f7f7f"; "#bcbd22"; "#17becf" ]

  let renderCols (width, height) topLabel trigger state = 
    if state.Completed && state.CompletionStep < 1.0 then
      window.setTimeout((fun () -> trigger Animate), 50) |> ignore
    let chart = 
      Axes(true, true,
        Interactive
          ( ( if state.Completed then []
              else
                [ EventHandler.MouseMove(fun evt (Cat(x, _), Cont y) ->
                    if (int evt.buttons) &&& 1 = 1 then trigger (Update(x, y)) )
                  EventHandler.MouseDown(fun evt (Cat(x, _), Cont y) ->
                    trigger (Update(x, y)) )
                  EventHandler.TouchStart(fun evt (Cat(x, _), Cont y) ->
                    trigger (Update(x, y)) )
                  EventHandler.TouchMove(fun evt (Cat(x, _), Cont y) ->
                    trigger (Update(x, y)) ) ] ),
            Style
              ( (fun s -> if state.Completed then s else { s with Cursor = "row-resize" }),
                (Layered [
                  yield Stack
                    ( Horizontal, 
                      [ for clr, (lbl, value) in Seq.zip vega10 state.Data -> 
                          let sh = Style((fun s -> { s with Fill = Solid(0.2, HTML "#a0a0a0") }), Column(CA lbl, CO state.Maximum )) 
                          Shape.Padding((0., 10., 0., 10.), sh) ])
                  yield Stack
                    ( Horizontal, 
                      [ for clr, (lbl, value) in Seq.zip vega10 state.Data -> 
                          let alpha, value = 
                            match state.Completed, state.Guesses.TryFind lbl with
                            | true, Some guess -> 0.6, state.CompletionStep * value + (1.0 - state.CompletionStep) * guess
                            | _, Some v -> 0.6, v
                            | _, None -> 0.2, state.Default
                          let sh = Style((fun s -> { s with Fill = Solid(alpha, HTML clr) }), Column(CA lbl, CO value)) 
                          Shape.Padding((0., 10., 0., 10.), sh) ])
                  for clr, (lbl, value) in Seq.zip vega10 state.Data do
                    match state.Guesses.TryFind lbl with
                    | None -> () 
                    | Some guess ->
                        let line = Line [ CAR(CA lbl, 0.0), COV (CO guess); CAR(CA lbl, 1.0), COV (CO guess) ]
                        yield Style(
                          (fun s -> 
                            { s with
                                StrokeColor = (1.0, HTML clr)
                                StrokeWidth = Pixels 4
                                StrokeDashArray = [ Integer 5; Integer 5 ] }), 
                          Shape.Padding((0., 10., 0., 10.), line))
                  match topLabel with
                  | None -> ()
                  | Some lbl ->
                      let x = CAR(CA (fst state.Data.[state.Data.Length/2]), if state.Data.Length % 2 = 0 then 0.0 else 0.5)
                      let y = COV(CO (state.Maximum * 0.9))
                      yield Style(
                        (fun s -> { s with Font = "13pt sans-serif"; Fill=Solid(1.0, HTML "#808080"); StrokeColor=(0.0, RGB(0,0,0)) }),
                        Text(x, y, VerticalAlign.Baseline, HorizontalAlign.Center, lbl) )
                ]) )))

    h?div ["style"=>"text-align:center;padding-top:20px"] [
      Compost.createSvg (width, height) chart
      h?div ["style"=>"padding-bottom:20px"] [
        h?button [
            yield "type" => "button"
            yield "click" =!> fun _ _ -> trigger ShowResults
            if state.Guesses.Count <> state.Data.Length then
              yield "disabled" => "disabled"
          ] [ text "Show me how I did" ]
        ]
    ]


  let renderBars (width, height) topLabel trigger state = 
    if state.Completed && state.CompletionStep < 1.0 then
      window.setTimeout((fun () -> trigger Animate), 50) |> ignore
    let chart = 
      Axes(true, false, 
        Interactive
          ( ( if state.Completed then []
              else
                [ EventHandler.MouseMove(fun evt (Cont x, Cat(y, _)) ->
                    if (int evt.buttons) &&& 1 = 1 then trigger (Update(y, x)) )
                  EventHandler.MouseDown(fun evt (Cont x, Cat(y, _)) ->
                    trigger (Update(y, x)) )
                  EventHandler.TouchStart(fun evt (Cont x, Cat(y, _)) ->
                    trigger (Update(y, x)) )
                  EventHandler.TouchMove(fun evt (Cont x, Cat(y, _)) ->
                    trigger (Update(y, x)) ) ] ),
            Style
              ( (fun s -> if state.Completed then s else { s with Cursor = "col-resize" }),
                (Layered [
                  yield Scale(Some(CO 0., CO state.Maximum), None, 
                    Stack
                      ( Vertical, 
                        [ for clr, (lbl, value) in Seq.zip vega10 state.Data -> 
                            let sh = Style((fun s -> { s with Fill = Solid(0.2, HTML "#a0a0a0") }), Bar(CO state.Maximum, CA lbl)) 
                            Shape.Padding((10., 0., 10., 0.), sh) ]))
                  yield Stack
                    ( Vertical, 
                      [ for clr, (lbl, value) in Seq.zip vega10 state.Data -> 
                          let alpha, value = 
                            match state.Completed, state.Guesses.TryFind lbl with
                            | true, Some guess -> 0.6, state.CompletionStep * value + (1.0 - state.CompletionStep) * guess
                            | _, Some v -> 0.6, v
                            | _, None -> 0.2, state.Default
                          let sh = Style((fun s -> { s with Fill = Solid(alpha, HTML clr) }), Bar(CO value, CA lbl)) 
                          Shape.Padding((10., 0., 10., 0.), sh) ])

                  for clr, (lbl, _) in Seq.zip vega10 state.Data do 
                      let x = COV(CO (state.Maximum * 0.95))
                      let y = CAR(CA lbl, 0.5)
                      yield Style(
                        (fun s -> { s with Font = "13pt sans-serif"; Fill=Solid(1.0, HTML clr); StrokeColor=(0.0, RGB(0,0,0)) }),
                        Text(x, y, VerticalAlign.Baseline, HorizontalAlign.End, lbl) )

                  for clr, (lbl, value) in Seq.zip vega10 state.Data do
                    match state.Guesses.TryFind lbl with
                    | None -> () 
                    | Some guess ->
                        let line = Line [ COV (CO guess), CAR(CA lbl, 0.0); COV (CO guess), CAR(CA lbl, 1.0) ]
                        yield Style(
                          (fun s -> 
                            { s with
                                StrokeColor = (1.0, HTML clr)
                                StrokeWidth = Pixels 4
                                StrokeDashArray = [ Integer 5; Integer 5 ] }), 
                          Shape.Padding((10., 0., 10., 0.), line))
                  match topLabel with
                  | None -> ()
                  | Some lbl ->
                      let x = COV(CO (state.Maximum * 0.9))
                      let y = CAR(CA (fst state.Data.[state.Data.Length/2]), if state.Data.Length % 2 = 0 then 0.0 else 0.5)
                      yield Style(
                        (fun s -> { s with Font = "13pt sans-serif"; Fill=Solid(1.0, HTML "#808080"); StrokeColor=(0.0, RGB(0,0,0)) }),
                        Text(x, y, VerticalAlign.Baseline, HorizontalAlign.Center, lbl) )
                ]) )))

    h?div ["style"=>"text-align:center;padding-top:20px"] [
      Compost.createSvg (width, height) chart
      h?div ["style"=>"padding-bottom:20px"] [
        h?button [
            yield "type" => "button"
            yield "click" =!> fun _ _ -> trigger ShowResults
            if state.Guesses.Count <> state.Data.Length then
              yield "disabled" => "disabled"
          ] [ text "Show me how I did" ]
        ]
    ]


open TheGamma.Series
open TheGamma.Common

type YouGuessColsBarsKind = Cols | Bars
type YouGuessColsBars =
  { kind : YouGuessColsBarsKind
    data : series<string, float> 
    maxValue : float option
    topLabel : string option }
  member y.setLabel(top) = { y with topLabel = Some top }
  member y.setMaximum(max) = { y with maxValue = Some max }
  member y.show(outputId) =   
    async { 
      let id = "container" + System.Guid.NewGuid().ToString().Replace("-", "")
      h?div ["id" => id] [ ] |> renderTo (document.getElementById(outputId))        

      // Get data & wait until the element is created
      let! data = y.data.data |> Async.AwaitFuture 
      let mutable i = 10
      while i > 0 && document.getElementById(id) = null do
        do! Async.Sleep(10)
        i <- i - 1
      let element = document.getElementById(id)
      let size = element.clientWidth, max 400. (element.clientWidth / 2.) 
      let! data = y.data.data |> Async.AwaitFuture 
      do
        try
          let render = match y.kind with Bars -> YouGuessColsHelpers.renderBars | Cols -> YouGuessColsHelpers.renderCols
          Compost.app outputId 
            (YouGuessColsHelpers.initState data y.maxValue) (render size y.topLabel) YouGuessColsHelpers.update
        with e ->
          Log.exn("GUI", "Interactive rendering failed: %O", e) } |> Async.StartImmediate

type youguess = 
  static member columns(data:series<string, float>) = 
    { YouGuessColsBars.data = data; topLabel = None; kind = Cols; maxValue = None }
  static member bars(data:series<string, float>) = 
    { YouGuessColsBars.data = data; topLabel = None; kind = Bars; maxValue = None  }
