﻿USE [FalconsFactionMonitor]

-- TODO: Set parameter values here.

EXECUTE [dbo].[GetFactionInfluenceHistory] 
   @FactionName
  ,@StartDate
  ,@EndDate
--GO