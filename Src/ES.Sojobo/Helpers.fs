﻿namespace ES.Sojobo

open System
open System.Reflection
open ES.Sojobo.Model
open B2R2
open B2R2.FrontEnd
open B2R2.BinFile.PE
open B2R2.BinFile
open System.Reflection.PortableExecutable
open System.Collections.Generic
open System.Runtime.InteropServices

module Helpers = 
    let toArray(bitVector: BitVector) =
        let size = int32 <| BitVector.getType bitVector
        let value = BitVector.getValue bitVector
        match size with        
        | 8 -> [|byte value|]
        | 16 -> BitConverter.GetBytes(uint16 value)
        | 32 -> BitConverter.GetBytes(uint32 value)
        | 64 -> BitConverter.GetBytes(uint64 value)
        | _ -> failwith("Unexpected size: " + string size)
        
    let getType(regType: RegType) =
        match (RegType.toBitWidth regType) with
        | 1 -> EmulatedType.Bit
        | 8 -> EmulatedType.Byte
        | 16 -> EmulatedType.Word
        | 32 -> EmulatedType.DoubleWord
        | 64 -> EmulatedType.QuadWord
        | 128 -> EmulatedType.XmmWord
        | 256 -> EmulatedType.YmmWord
        | 512 -> EmulatedType.ZmmWord
        | _ -> failwith("Invalid reg type size: " + regType.ToString())

    let getSize(emuType: EmulatedType) =
        match emuType with
        | EmulatedType.Bit -> 1
        | EmulatedType.Byte -> 8
        | EmulatedType.Word -> 16
        | EmulatedType.DoubleWord -> 32
        | EmulatedType.QuadWord -> 64
        | EmulatedType.XmmWord -> 128
        | EmulatedType.YmmWord -> 256
        | EmulatedType.ZmmWord -> 512

    let getTypeSize =
        getType >> getSize

    let getTempName(index: String, emuType: EmulatedType) =
        let size =  getSize(emuType)
        "T_" + string index + ":" + string size

    let getPe(handler: BinHandler) =
        let fileInfo = handler.FileInfo
        let pe = fileInfo.GetType().GetField("pe", BindingFlags.NonPublic ||| BindingFlags.Instance)
        if pe <> null then Some(pe.GetValue(fileInfo) :?> PE)
        else None

    let getSectionPermission(sectionHeader: SectionHeader) =
        let characteristics = sectionHeader.SectionCharacteristics
        let mutable permission: Permission option = None
        
        if characteristics.HasFlag(SectionCharacteristics.MemRead) then 
            permission <- Some Permission.Readable

        if characteristics.HasFlag(SectionCharacteristics.MemWrite) then 
            permission <-
                match permission with
                | Some p -> p ||| Permission.Writable
                | None -> Permission.Writable
                |> Some

        if characteristics.HasFlag(SectionCharacteristics.MemExecute) then 
            permission <-
                match permission with
                | Some p -> p ||| Permission.Executable
                | None -> Permission.Executable
                |> Some

        Option.defaultValue Permission.Readable permission

    let getFunctionKeyName(functioName: String, libraryName: String) =
        let keyName = (libraryName + "::" + functioName).ToLower()
        keyName.Replace(".dll", String.Empty)

    let rec getFieldArrayLength(field: FieldInfo, pointerSize: Int32, computedSize: Dictionary<Type, Int32>) =
        let arrayLength = field.GetCustomAttribute<MarshalAsAttribute>().SizeConst
        let elementType = field.FieldType.GetElementType()
        arrayLength * calculateSize(elementType, pointerSize, computedSize)

    and private calculateSize(objectType: Type, pointerSize: Int32, computedSize: Dictionary<Type, Int32>) =
        let flags = BindingFlags.Instance ||| BindingFlags.NonPublic ||| BindingFlags.Public        

        if computedSize.ContainsKey(objectType) then
            computedSize.[objectType]
            
        elif objectType.IsArray then
            let arrayLength = objectType.GetCustomAttribute<MarshalAsAttribute>().SizeConst
            let elementType = objectType.GetElementType()
            arrayLength * calculateSize(elementType, pointerSize, computedSize)

        elif objectType.IsClass || objectType.IsValueType then            
            objectType.GetFields(flags)
            |> Array.sumBy(fun field ->
                let t = field.FieldType
                if t.IsArray then getFieldArrayLength(field, pointerSize, computedSize)
                elif t.IsPrimitive || t.IsUnicodeClass || t.IsEnum then Marshal.SizeOf(t)
                elif t.IsClass then pointerSize / 8                
                elif t.IsValueType then calculateSize(t, pointerSize, computedSize)                
                else failwith("Unable to get size of type: " + t.FullName)
            )
            |> fun totalSize ->
                computedSize.[objectType] <- totalSize
                totalSize
        else
            failwith("Unable to get size of type: " + objectType.FullName)

    let deepSizeOf(objectType: Type, pointerSize: Int32) =
        calculateSize(objectType, pointerSize, new Dictionary<Type, Int32>())