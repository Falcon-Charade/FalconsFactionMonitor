USE [FalconsFactionMonitor]

-- TODO: Set parameter values here.

EXECUTE [dbo].[GetSystemInfluenceHistory] 
   @SystemName
  ,@StartDate
  ,@EndDate
--GO