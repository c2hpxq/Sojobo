﻿namespace ES.Sojobo

open System
open ES.Sojobo.Model
open B2R2.BinFile
open B2R2.FrontEnd

type IProcessContainer =
    interface 
        /// Get the memory manager associated with the process
        abstract Memory: MemoryManager with get

        /// Get the CPU object associated with the process
        abstract Cpu: Cpu with get

        /// A list of the hadnel owned by the process
        abstract Handles: Handle array with get

        /// Return the Pid valued of the emulated process
        abstract Pid: UInt32 with get, set

        /// Return the actual program counter value
        abstract ProgramCounter: EmulatedValue with get
        
        /// get the memory region that is currenlty being executed by the process
        abstract GetActiveMemoryRegion: unit -> MemoryRegion

        /// get a list of symbols that are imported by the binary
        abstract GetImportedFunctions: unit -> Symbol seq

        /// get the instruction at the given address
        abstract GetInstruction: address:UInt64 -> Instruction    

        /// get the next instruction that is going to be executed
        abstract GetInstruction: unit -> Instruction        

        /// Return an array of addresses related to the current call stack
        /// The array is composed by walking the stack, if it corrupted, this 
        /// value will be corrupted too
        abstract GetCallStack: unit -> UInt64 array

        /// Get the size in bit of the pointer for the current process
        abstract PointerSize: unit -> Int32 with get
        
        /// Try to get a symbol defined at the specified address
        abstract TryGetSymbol: UInt64 -> Symbol option

        /// Add a new symbol to the list
        abstract SetSymbol: Symbol -> unit

        /// The name of the file used to create the process
        abstract FileName: String with get
    end