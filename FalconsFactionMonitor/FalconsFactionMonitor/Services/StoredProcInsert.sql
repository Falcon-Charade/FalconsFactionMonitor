USE [FalconsFactionMonitor]

-- TODO: Set parameter values here.

EXECUTE [dbo].[ImportSystemHistory] 
   @SystemName
  ,@FactionName
  ,@Influence
  ,@State
  ,@PlayerFaction
  ,@LastUpdated
--GO