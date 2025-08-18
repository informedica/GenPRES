#!/bin/bash

# debugTests - Run all test projects with debug mode
# Usage: ./debugTests.sh

echo "Running all test projects in debug mode..."
echo "=========================================="

# Alternative Expecto options you can use:
# --debug --summary --sequenced          # Current: Detailed output with summary, sequential execution
# --debug --summary                       # Detailed output with summary, parallel execution
# --summary                               # Normal output with summary only
# --filter "Agent Logging"                # Run only tests matching filter
# --list-tests                            # List all tests without running them
# --summary-location                      # Include source code locations in summary

# Array of test projects
test_projects=(
    "tests/Informedica.Utils.Tests/Informedica.Utils.Tests.fsproj"
    "tests/Informedica.Agents.Tests/Informedica.Agents.Tests.fsproj"
    "tests/Informedica.Logging.Tests/Informedica.Logging.Tests.fsproj"
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
    dotnet run --project "$project" -- --debug --summary --sequenced
    
    # Check if the command succeeded
    if [ $? -ne 0 ]; then
        echo "ERROR: Failed to run $project"
        exit 1
    fi
done

echo ""
echo "All test projects completed successfully!"
