#r "nuget: Expecto"
#r "nuget: Expecto.FsCheck"
#r "nuget: Unquote"

#load "load.fsx"

open Informedica.Agents.Lib
open Informedica.Utils.Lib
open Expecto
open FsCheck
open System
open System.IO
open System.Text
open System.Threading



// Helper functions for testing
module TestHelpers =
    
    let createTempFile () =
        let tempPath = Path.GetTempFileName()
        tempPath

    let createTempDirectory () =
        let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        Directory.CreateDirectory(tempDir) |> ignore
        tempDir

    let deleteFileIfExists path =
        try
            if File.Exists path then
                File.Delete path
        with _ -> ()

    let deleteDirIfExists path =
        try
            if Directory.Exists path then
                Directory.Delete(path, true)
        with _ -> ()

    let readAllLines path =
        try
            if File.Exists path then
                File.ReadAllLines(path)
            else
                [||]
        with _ -> [||]

    let readAllText path =
        try
            if File.Exists path then
                File.ReadAllText(path)
            else
                ""
        with _ -> ""

    let waitForFileWrite () = Thread.Sleep(100)

open TestHelpers

// Basic functionality tests
let basicTests =
    testList "Basic FileWriterAgent Operations" [
        
        test "create agent should succeed" {
            use writer = FileWriterAgent.create()
            Expect.isTrue (writer <> Unchecked.defaultof<_>) "Agent should be created"
        }
        
        testAsync "append single line should work" {
            let tempFile = createTempFile()
            
            try
                use writer = FileWriterAgent.create()
                
                writer 
                |> FileWriterAgent.append tempFile [|"Hello, World!"|]
                |> FileWriterAgent.flush
                |> ignore
                
                waitForFileWrite()
                
                let content = readAllLines tempFile
                Expect.equal content [|"Hello, World!"|] "Should write single line"
                
            finally
                deleteFileIfExists tempFile
        }
        
        testAsync "append multiple lines should work" {
            let tempFile = createTempFile()
            
            try
                use writer = FileWriterAgent.create()
                
                let lines = [|"Line 1"; "Line 2"; "Line 3"|]
                writer 
                |> FileWriterAgent.append tempFile lines
                |> FileWriterAgent.flush
                |> ignore
                
                waitForFileWrite()
                
                let content = readAllLines tempFile
                Expect.equal content lines "Should write all lines"
                
            finally
                deleteFileIfExists tempFile
        }
        
        testAsync "multiple appends should accumulate" {
            let tempFile = createTempFile()
            
            try
                use writer = FileWriterAgent.create()
                
                writer 
                |> FileWriterAgent.append tempFile [|"First"|]
                |> FileWriterAgent.append tempFile [|"Second"|]
                |> FileWriterAgent.append tempFile [|"Third"|]
                |> FileWriterAgent.flush
                |> ignore
                
                waitForFileWrite()
                
                let content = readAllLines tempFile
                Expect.equal content [|"First"; "Second"; "Third"|] "Should accumulate lines"
                
            finally
                deleteFileIfExists tempFile
        }
        
        testAsync "append to non-existent file should create it" {
            let tempDir = createTempDirectory()
            let tempFile = Path.Combine(tempDir, "newfile.txt")
            
            try
                use writer = FileWriterAgent.create()
                
                writer 
                |> FileWriterAgent.append tempFile [|"Created file"|]
                |> FileWriterAgent.flush
                |> ignore
                
                waitForFileWrite()
                
                Expect.isTrue (File.Exists tempFile) "Should create file"
                let content = readAllLines tempFile
                Expect.equal content [|"Created file"|] "Should write content"
                
            finally
                deleteDirIfExists tempDir
        }
    ]

// Clear functionality tests
let clearTests =
    testList "Clear Operations" [
        
        testAsync "clear should empty existing file" {
            let tempFile = createTempFile()
            
            try
                // Write initial content
                File.WriteAllLines(tempFile, [|"Initial"; "Content"|])
                
                use writer = FileWriterAgent.create()
                
                writer 
                |> FileWriterAgent.clear tempFile
                |> FileWriterAgent.flush
                |> ignore
                
                waitForFileWrite()
                
                let content = readAllText tempFile
                Expect.equal content "" "Should be empty after clear"
                
            finally
                deleteFileIfExists tempFile
        }
        
        testAsync "clear non-existent file should create empty file" {
            let tempDir = createTempDirectory()
            let tempFile = Path.Combine(tempDir, "cleartest.txt")
            
            try
                use writer = FileWriterAgent.create()
                
                writer 
                |> FileWriterAgent.clear tempFile
                |> FileWriterAgent.flush
                |> ignore
                
                waitForFileWrite()
                
                Expect.isTrue (File.Exists tempFile) "Should create file"
                let content = readAllText tempFile
                Expect.equal content "" "Should be empty"
                
            finally
                deleteDirIfExists tempDir
        }
        
        testAsync "clear then append should work" {
            let tempFile = createTempFile()
            
            try
                // Write initial content
                File.WriteAllLines(tempFile, [|"Old content"|])
                
                use writer = FileWriterAgent.create()
                
                writer 
                |> FileWriterAgent.clear tempFile
                |> FileWriterAgent.append tempFile [|"New content"|]
                |> FileWriterAgent.flush
                |> ignore
                
                waitForFileWrite()
                
                let content = readAllLines tempFile
                Expect.equal content [|"New content"|] "Should only have new content"
                
            finally
                deleteFileIfExists tempFile
        }
    ]

// Multiple file tests
let multiFileTests =
    testList "Multiple File Operations" [
        
        testAsync "should handle multiple files independently" {
            let tempFile1 = createTempFile()
            let tempFile2 = createTempFile()
            
            try
                use writer = FileWriterAgent.create()
                
                writer 
                |> FileWriterAgent.append tempFile1 [|"File 1 content"|]
                |> FileWriterAgent.append tempFile2 [|"File 2 content"|]
                |> FileWriterAgent.flush
                |> ignore
                
                waitForFileWrite()
                
                let content1 = readAllLines tempFile1
                let content2 = readAllLines tempFile2
                
                Expect.equal content1 [|"File 1 content"|] "File 1 should have correct content"
                Expect.equal content2 [|"File 2 content"|] "File 2 should have correct content"
                
            finally
                deleteFileIfExists tempFile1
                deleteFileIfExists tempFile2
        }
        
        testAsync "clear should only affect target file" {
            let tempFile1 = createTempFile()
            let tempFile2 = createTempFile()
            
            try
                use writer = FileWriterAgent.create()
                
                // Write to both files
                writer 
                |> FileWriterAgent.append tempFile1 [|"File 1"|]
                |> FileWriterAgent.append tempFile2 [|"File 2"|]
                |> FileWriterAgent.flush
                |> ignore
                
                waitForFileWrite()
                
                // Clear only file 1
                writer 
                |> FileWriterAgent.clear tempFile1
                |> FileWriterAgent.flush
                |> ignore
                
                waitForFileWrite()
                
                let content1 = readAllText tempFile1
                let content2 = readAllLines tempFile2
                
                Expect.equal content1 "" "File 1 should be empty"
                Expect.equal content2 [|"File 2"|] "File 2 should be unchanged"
                
            finally
                deleteFileIfExists tempFile1
                deleteFileIfExists tempFile2
        }
    ]

// Encoding tests
let encodingTests =
    testList "Encoding Handling" [
        
        testAsync "should handle UTF-8 content correctly" {
            let tempFile = createTempFile()
            
            try
                use writer = FileWriterAgent.create()
                
                let unicodeContent = [|"Hello 世界"; "Café ñoño"; "🚀 rocket"|]
                
                writer 
                |> FileWriterAgent.append tempFile unicodeContent
                |> FileWriterAgent.flush
                |> ignore
                
                waitForFileWrite()
                
                let content = readAllLines tempFile
                Expect.equal content unicodeContent "Should handle Unicode correctly"
                
            finally
                deleteFileIfExists tempFile
        }
        
        testAsync "should preserve existing file encoding" {
            let tempFile = createTempFile()
            
            try
                // Write initial content with specific encoding
                File.WriteAllLines(tempFile, [|"Initial content"|], Encoding.UTF8)
                
                use writer = FileWriterAgent.create()
                
                writer 
                |> FileWriterAgent.append tempFile [|"Appended content"|]
                |> FileWriterAgent.flush
                |> ignore
                
                waitForFileWrite()
                
                let content = readAllLines tempFile
                Expect.equal content [|"Initial content"; "Appended content"|] "Should preserve and append correctly"
                
            finally
                deleteFileIfExists tempFile
        }
    ]

// Error handling tests
let errorHandlingTests =
    testList "Error Handling" [
        
        testAsync "should handle invalid path gracefully" {
            use writer = FileWriterAgent.create()
            
            // This should not crash the agent
            let invalidPath = "//invalid//path//file.txt"
            
            writer 
            |> FileWriterAgent.append invalidPath [|"test"|]
            |> FileWriterAgent.flush
            |> ignore
            
            // Agent should still be responsive
            let tempFile = createTempFile()
            
            try
                writer 
                |> FileWriterAgent.append tempFile [|"Valid operation"|]
                |> FileWriterAgent.flush
                |> ignore
                
                waitForFileWrite()
                
                let content = readAllLines tempFile
                Expect.equal content [|"Valid operation"|] "Agent should continue working after error"
                
            finally
                deleteFileIfExists tempFile
        }
        
        testAsync "should handle empty lines array" {
            let tempFile = createTempFile()
            
            try
                use writer = FileWriterAgent.create()
                
                writer 
                |> FileWriterAgent.append tempFile [||]
                |> FileWriterAgent.flush
                |> ignore
                
                waitForFileWrite()
                
                let content = readAllText tempFile
                Expect.equal content "" "Empty array should result in no content"
                
            finally
                deleteFileIfExists tempFile
        }
    ]

// Performance tests
let performanceTests =
    testList "Performance Tests" [
        
        testAsync "should handle large number of lines" {
            let tempFile = createTempFile()
            
            try
                use writer = FileWriterAgent.create()
                
                let largeContent = Array.init 1000 (fun i -> $"Line {i}")
                
                writer 
                |> FileWriterAgent.append tempFile largeContent
                |> FileWriterAgent.flush
                |> ignore
                
                waitForFileWrite()
                
                let content = readAllLines tempFile
                Expect.equal content.Length 1000 "Should handle large content"
                Expect.equal content.[0] "Line 0" "First line should be correct"
                Expect.equal content.[999] "Line 999" "Last line should be correct"
                
            finally
                deleteFileIfExists tempFile
        }
        
        testAsync "should handle rapid successive operations" {
            let tempFile = createTempFile()
            
            try
                use writer = FileWriterAgent.create()
                
                // Rapid fire operations
                for i in 1..100 do
                    writer 
                    |> FileWriterAgent.append tempFile [|$"Rapid {i}"|]
                    |> ignore
                
                writer |> FileWriterAgent.flush |> ignore
                waitForFileWrite()
                
                let content = readAllLines tempFile
                Expect.equal content.Length 100 "Should handle all rapid operations"
                
            finally
                deleteFileIfExists tempFile
        }
    ]

// Property-based tests
// Property-based tests
let propertyTests =
    testList "Property-based Tests" [
        
        testProperty "all written lines should be readable" <| fun (lines: string list) ->
            let validLines = 
                lines 
                |> List.filter (fun s -> s <> null)
                |> List.map (fun s -> s.Replace("\n", "").Replace("\r", ""))
                |> List.filter (fun s -> s.Length <= 50)
                |> List.truncate 25
            
            (not (List.isEmpty validLines)) ==> lazy (
                let tempFile = createTempFile()
                
                try
                    use writer = FileWriterAgent.create()
                    
                    let linesArray = List.toArray validLines
                    writer 
                    |> FileWriterAgent.append tempFile linesArray
                    |> FileWriterAgent.flush
                    |> ignore
                    
                    waitForFileWrite()
                    
                    let content = readAllLines tempFile
                    content = linesArray
                    
                finally
                    deleteFileIfExists tempFile
            )

        testProperty "clear always results in empty file" <| fun (initialContent: string list) ->
            let validContent = 
                initialContent 
                |> List.filter (fun s -> s <> null)
                |> List.map (fun s -> s.Replace("\n", "").Replace("\r", ""))
                |> List.filter (fun s -> s.Length <= 50)
                |> List.truncate 20
            
            true ==> lazy (  // Always run, even with empty content
                let tempFile = createTempFile()
                
                try
                    use writer = FileWriterAgent.create()
                    
                    // Write initial content if any
                    if not (List.isEmpty validContent) then
                        writer 
                        |> FileWriterAgent.append tempFile (List.toArray validContent)
                        |> FileWriterAgent.flush
                        |> ignore
                    
                    // Clear the file
                    writer 
                    |> FileWriterAgent.clear tempFile
                    |> FileWriterAgent.flush
                    |> ignore
                    
                    waitForFileWrite()
                    
                    let content = readAllText tempFile
                    content = ""
                    
                finally
                    deleteFileIfExists tempFile
            )
        
        testProperty "append is associative" <| fun (lines1: string list) (lines2: string list) ->
            let validLines1 = 
                lines1 
                |> List.filter (fun s -> s <> null)
                |> List.map (fun s -> s.Replace("\n", "").Replace("\r", ""))
                |> List.filter (fun s -> s.Length <= 50)
                |> List.truncate 15
                
            let validLines2 = 
                lines2 
                |> List.filter (fun s -> s <> null)
                |> List.map (fun s -> s.Replace("\n", "").Replace("\r", ""))
                |> List.filter (fun s -> s.Length <= 50)
                |> List.truncate 15
            
            (not (List.isEmpty validLines1) || not (List.isEmpty validLines2)) ==> lazy (
                let tempFile1 = createTempFile()
                let tempFile2 = createTempFile()
                
                try
                    use writer = FileWriterAgent.create()
                    
                    // Method 1: append all at once
                    let allLines = validLines1 @ validLines2
                    if not (List.isEmpty allLines) then
                        writer 
                        |> FileWriterAgent.append tempFile1 (List.toArray allLines)
                        |> FileWriterAgent.flush
                        |> ignore
                    
                    // Method 2: append separately
                    if not (List.isEmpty validLines1) then
                        writer 
                        |> FileWriterAgent.append tempFile2 (List.toArray validLines1)
                        |> ignore
                    if not (List.isEmpty validLines2) then
                        writer 
                        |> FileWriterAgent.append tempFile2 (List.toArray validLines2)
                        |> ignore
                    writer |> FileWriterAgent.flush |> ignore
                    
                    waitForFileWrite()
                    
                    let content1 = readAllLines tempFile1
                    let content2 = readAllLines tempFile2
                    content1 = content2
                    
                finally
                    deleteFileIfExists tempFile1
                    deleteFileIfExists tempFile2
            )
    ]

// Async operation tests
let asyncTests =
    testList "Async Operations" [
        
        testAsync "flushAsync should work" {
            let tempFile = createTempFile()
            
            try
                use writer = FileWriterAgent.create()
                
                writer 
                |> FileWriterAgent.append tempFile [|"Async test"|]
                |> ignore
                
                do! FileWriterAgent.flushAsync writer
                
                let content = readAllLines tempFile
                Expect.equal content [|"Async test"|] "Should flush asynchronously"
                
            finally
                deleteFileIfExists tempFile
        }
        
        testAsync "stopAsync should work" {
            let tempFile = createTempFile()
            
            try
                use writer = FileWriterAgent.create()
                
                writer 
                |> FileWriterAgent.append tempFile [|"Stop test"|]
                |> ignore
                
                do! FileWriterAgent.stopAsync writer
                
                let content = readAllLines tempFile
                Expect.equal content [|"Stop test"|] "Should stop and flush content"
                
            finally
                deleteFileIfExists tempFile
        }
    ]

// Main test suite
let allTests =
    testList "FileWriterAgent Tests" [
        basicTests
        clearTests
        multiFileTests
        encodingTests
        errorHandlingTests
        performanceTests
        propertyTests
        asyncTests
    ]

// Run tests
runTestsWithCLIArgs [] [|"--summary"|] asyncTests