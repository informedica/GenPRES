# Informedica.GenCore.Lib

A foundational F# library providing core functionality for the Informedica suite of medical calculation applications. This library contains essential types, calculations, and utilities used across other Informedica projects.

## Overview

GenCore.Lib serves as the foundational layer for medical and pharmaceutical calculations, providing:

- **Core Types**: Essential data structures for medical calculations
- **Value Units**: Type-safe handling of physical quantities with units of measure
- **MinMax Ranges**: Constraint modeling for medical parameters
- **Patient Models**: Patient demographic and clinical data structures
- **Mathematical Operations**: Specialized calculations for medical contexts
- **Validation**: Input validation using functional validation patterns

## Key Components

### ValueUnit.fs
Provides type-safe representation of values with their associated units of measure. Essential for medical calculations where unit errors can be dangerous.

```fsharp
// Example: Creating a dose with proper units
let dose = ValueUnit.create 5.0 Units.Weight.milliGram
```

### MinMax.fs
Implements constraint ranges for medical parameters, supporting minimum/maximum bounds with inclusive/exclusive boundaries.

```fsharp
// Example: Age range constraints
let ageRange = MinMax.create (Some 18) (Some 65) true true
```

### Patient.fs
Defines patient data structures including demographics, measurements, and clinical parameters.

```fsharp
// Patient with weight and age constraints
type Patient = {
    Id: PatientId
    Weight: ValueUnit option
    Age: int option
    // ... other properties
}
```

### Calculations.fs
Contains mathematical operations and algorithms specific to medical and pharmaceutical calculations.

### Measures.fs
Defines units of measure and measurement-related functionality for the medical domain.

### Validus.fs
Provides functional validation patterns for input validation and business rule enforcement.

### Aether.fs
Implements lens-based functional programming patterns for immutable data manipulation.

## Dependencies

- **MathNet.Numerics**: For advanced mathematical operations
- **F# Core Libraries**: Leveraging F#'s type system and functional programming features

## Usage

This library is typically consumed by other Informedica projects:

```fsharp
open Informedica.GenCore.Lib

// Create a patient with weight
let patient = {
    Patient.empty with
        Weight = ValueUnit.create 70.0 Units.Weight.kiloGram |> Some
}

// Define dose constraints
let doseRange = MinMax.create (Some 5.0) (Some 20.0) true false
```

## Project Structure

```
src/Informedica.GenCore.Lib/
├── Aether.fs              # Lens-based data manipulation
├── Calculations.fs        # Medical calculations
├── Measures.fs           # Units of measure definitions
├── MinMax.fs             # Range and constraint modeling
├── Patient.fs            # Patient data structures
├── Validus.fs            # Validation patterns
├── ValueUnit.fs          # Type-safe value-unit pairs
└── Scripts/              # Development and testing scripts
    ├── load.fsx
    └── Scripts.fsx
```

## Development

### Building
```bash
dotnet build
```

### Testing
Tests are typically located in separate test projects that reference this library.

### Scripts
The `Scripts/` folder contains F# scripts for interactive development and testing:

- `load.fsx`: Loads the library for interactive development
- `Scripts.fsx`: Contains development and testing scripts

## Integration

This library integrates with other Informedica projects:

- **Informedica.GenUnits.Lib**: Provides the units of measure system
- **Informedica.GenSolver.Lib**: Uses core types for constraint solving
- **Informedica.GenOrder.Lib**: Builds upon core types for medical orders

## Safety and Validation

The library emphasizes type safety and validation:

- **Units of Measure**: Prevents unit-related calculation errors
- **Option Types**: Explicit handling of missing or invalid data
- **Validation Patterns**: Functional validation with composable rules
- **Immutable Data**: Prevents accidental data modification

## Contributing

When contributing to this core library:

1. Maintain backward compatibility
2. Add comprehensive tests for new functionality
3. Follow F# coding conventions
4. Document public APIs with XML documentation
5. Consider the impact on dependent projects

## License

Part of the Informedica suite of medical calculation libraries.