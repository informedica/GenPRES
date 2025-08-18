#!/bin/bash

# debugTests - Run all test projects with debug mode
# Usage: ./debugTests.sh

echo "Running all test projects in debug mode..."
echo "=========================================="

# Array of test projects
test_projects=(
    "tests/Informedica.Utils.Tests/Informedica.Utils.Tests.fsproj"
    "tests/Informedica.Agents.Tests/Informedica.Agents.Tests.fsproj"
    "tests/Informedica.GenUnits.Tests/Informedica.GenUnits.Tests.fsproj"
    "tests/Informedica.GenCore.Tests/Informedica.GenCore.Tests.fsproj"
    "tests/Informedica.GenSolver.Tests/Informedica.GenSolver.Tests.fsproj"
    "tests/Informedica.GenForm.Tests/Informedica.GenForm.Tests.fsproj"
    "tests/Informedica.GenOrder.Tests/Informedica.GenOrder.Tests.fsproj"
#    "tests/Informedica.ZIndex.Tests/Informedica.ZIndex.Tests.fsproj"
    "tests/Server/Server.Tests.fsproj"
)

# Run each test project
for project in "${test_projects[@]}"; do
    echo ""
    echo "Running: $project"
    echo "----------------------------------------"
    dotnet run --project "$project" --debug
    
    # Check if the command succeeded
    if [ $? -ne 0 ]; then
        echo "ERROR: Failed to run $project"
        exit 1
    fi
done

echo ""
echo "All test projects completed successfully!"
