# Folder to store all cached objects

This directory is used to cache generated and downloaded data used by GenPRES, such as medication reference data, pre-processed rule sets, and spreadsheet/CSV downloads.

Only demo and test cache data are checked into the repository. Production cache data are never committed due to licensing and confidentiality constraints and must be generated locally in each environment.

The ZIndex medication data load as one set of four files: `substance`, `product`, `rule` and `group`. The licensed `*.cache` set loads when all four files are here; otherwise the `*.demo` set loads. In production (`GENPRES_PROD=1`) an incomplete `*.cache` set refuses the start. To build a set, see `buildCache` in `src/Informedica.ZIndex.Lib/Scripts/Script.fsx`.
