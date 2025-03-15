
open System

Environment.SetEnvironmentVariable("GENPRES_LOG", "1")
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA")

#load "load.fsx"

let logger = Informedica.GenOrder.Lib.OrderLogger.logger

let path = $"{__SOURCE_DIRECTORY__}/log.txt"
logger.Start (Some path) Informedica.GenOrder.Lib.OrderLogger.Level.Informative

open Shared