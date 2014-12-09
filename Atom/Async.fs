﻿module Atom.Async

open System
open System.Threading
open System.Reactive
open System.Reactive.Linq

#nowarn "40"

type Microsoft.FSharp.Control.Async with 
    static member AwaitObservable (obs : IObservable<'T>) =
        let timeReceived = new Event<'T> ()
        let rec trigger t =
            timeReceived.Trigger t
            sub.Dispose ()
        and sub : IDisposable = obs.Subscribe<'T> (trigger)
        Async.AwaitEvent (timeReceived.Publish)

type System.Reactive.Linq.Observable with
  static member FromAsync (computation, ?cts : CancellationTokenSource) = 
      Observable.Create<'a> (Func<IObserver<'a>, Action>(fun o ->
        if o = null then nullArg "observer"
        let cts = defaultArg cts (new CancellationTokenSource ())
        let invoked = ref 0
        let cancelOrDispose cancel =
          if Interlocked.CompareExchange (invoked, 1, 0) = 0 then
            if cancel then cts.Cancel () else cts.Dispose ()
        let wrapper = async {
          try
            let res = ref Unchecked.defaultof<_>
            try
              let! result = computation
              res := result
            with e -> o.OnError (e)
            o.OnNext (!res)
            o.OnCompleted ()
          finally cancelOrDispose false }
        Async.StartImmediate (wrapper, cts.Token)
        Action(fun () -> cancelOrDispose true)))