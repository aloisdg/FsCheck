﻿namespace FsCheck.Test.Verify

open System
open System.Reflection
open System.Threading

open Xunit
open VerifyXunit

open FsCheck
open FsCheck.FSharp

[<UsesVerify>]
module VerifyGen =

    let seed = Random.CreateWithSeed(9854927542UL)
    let size = 50
    let nbSamples = 20
    
    let sample g = g |> Gen.sampleWithSeed seed size nbSamples

    let sampleSmall g = g |> Gen.sampleWithSeed seed (size/2) (nbSamples/2)

    let verify (anything:'T) =
        // Verify doesn't return a Task, exactly, it returns an awaitable.
        // But xunit requires a Task back. In C# you can just await it.
        // I couldn't find a less heavy-handed way of doing the same in F#.
        let awaiter = Verifier.Verify<'T>(anything)
                        .UseDirectory("Verified")
                        .ModifySerialization(fun t -> t.DontScrubDateTimes())
                        .GetAwaiter()
        async {
            use handle = new SemaphoreSlim(0)
            awaiter.OnCompleted(fun () -> ignore (handle.Release()))
            let! _ = handle.AvailableWaitHandle |> Async.AwaitWaitHandle
            return awaiter.GetResult() 
        } |> Async.StartAsTask

    let verifyGen (gen:Gen<'T>) =
        gen
        |> sample 
        |> verify

    [<Fact>]
    let ``choose(-100,100)``() =
        Gen.choose(-100,100)
        |> verifyGen

    [<Fact>]
    let ``choose64(-100,100)``() =
        Gen.choose64(-100L,100L)
        |> verifyGen

    [<Fact>]
    let ``arrayOf choose(-10,10)``() =
        Gen.choose(-10,10)
        |> Gen.arrayOf
        |> verifyGen

    [<Fact>]
    let ``listOf choose(-10,10)``() =
        Gen.choose(-10,10)
        |> Gen.listOf
        |> verifyGen

    type ShrinkVerify<'T> =
        { Original: 'T // original value that is being shrunk
          Success: array<'T> // array of shrinks, assuming all shrinks succeed
          Fail: array<'T> // array of shrinks, assuming all shrinks fail
        }

    let verifyArb (arb:Arbitrary<'T>) =
        let samples = arb.Generator |> sampleSmall
        samples
        |> Array.map(fun sample ->
            let success = ResizeArray<'T>()
            let mutable next = arb.Shrinker sample |> Seq.tryHead
            while next.IsSome do
                success.Add(next.Value)
                next <- arb.Shrinker next.Value |> Seq.tryHead

            let fail = arb.Shrinker sample |> Seq.toArray
            { Original = sample
              Success = success.ToArray()
              Fail = fail
            }
        )
        |> verify

    [<Fact>]
    let ``Int32``() =
        ArbMap.defaults
        |> ArbMap.arbitrary<Int32>
        |> verifyArb

    [<Fact>]
    let ``Double``() =
        ArbMap.defaults
        |> ArbMap.arbitrary<Double>
        |> verifyArb

    [<Fact>]
    let ``Array of Int32``() =
        ArbMap.defaults
        |> ArbMap.arbitrary<array<int>>
        |> verifyArb

    [<Fact>]
    let ``String``() =
        ArbMap.defaults
        |> ArbMap.arbitrary<String>
        |> verifyArb

    [<Fact>]
    let ``DateTimeOffset``() =
        ArbMap.defaults
        |> ArbMap.arbitrary<DateTimeOffset>
        |> verifyArb

    [<Fact>]
    let ``Map int,char``() =
        ArbMap.defaults
        |> ArbMap.arbitrary<Map<int,char>>
        |> verifyArb





        
// without this, attribute Verify refuses to work.
// Also, it automatically replaces anything that looks like the value with {ProjectDirectory},
// which we also never want.
[<AssemblyMetadataAttribute("Verify.ProjectDirectory", "anything that is unlikely to show up in values")>]
do ()