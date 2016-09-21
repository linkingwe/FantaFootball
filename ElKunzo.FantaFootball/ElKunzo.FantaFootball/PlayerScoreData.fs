﻿namespace ElKunzo.FantaFootball.Internal

open System
open System.Collections.Generic
open System.Data
open System.Data.Common
open Microsoft.SqlServer.Server

open ElKunzo.FantaFootball
open ElKunzo.FantaFootball.DataAccess
open ElKunzo.FantaFootball.External.FootballDataTypes
open ElKunzo.FantaFootball.External.WhoScoredTypes

module PlayerScoreData = 

    type T = {
        Id : int;
        FixtureId : int;
        PlayerId : int;
        TotalPoints : int;
        MinutesPlayed : int;
        GoalsScored : int;
        Assists : int;
        CleanSheet : bool;
        ShotsSaved : int;
        PenaltiesSaved : int;
        PenaltiesMissed : int;
        GoalsConceded : int;
        YellowCards : int;
        RedCard : int;
        OwnGoals : int;
    }



    type Cache (spName, mappingFunction, refreshInterval) =
        inherit BaseCacheWithRefreshTimer<T>(spName, mappingFunction, refreshInterval)

        override this.TryGetItem (id) = 
            if this.IsOutdated() then this.Update()
            this.PublicData |> Seq.tryFind (fun p -> p.Id = id)



    let mapFromSqlType (dataReader:DbDataReader) = 
        let idOrdinal = dataReader.GetOrdinal("fId")
        let fixtureIdOrdinal = dataReader.GetOrdinal("frFixtureId")
        let playerIdOrdinal = dataReader.GetOrdinal("frPlayerId")
        let totalPointsOrdinal = dataReader.GetOrdinal("fTotalPoints")
        let minutesPlayedOrdinal = dataReader.GetOrdinal("fMinutesPlayed")
        let goalsScoredOrdinal = dataReader.GetOrdinal("fGoalsScored")
        let assistsOrdinal = dataReader.GetOrdinal("fAssists")
        let cleanSheetOrdinal = dataReader.GetOrdinal("fCleanSheet")
        let shotsSavedOrdinal = dataReader.GetOrdinal("fShotsSaved")
        let penaltiesSavedOrdinal = dataReader.GetOrdinal("fPenaltiesSaved")
        let penaltiesMissedOrdinal = dataReader.GetOrdinal("fPenaltiesMissed")
        let goalsConcededOrdinal = dataReader.GetOrdinal("fGoalsConceded")
        let yellowCardsOrdinal = dataReader.GetOrdinal("fYellowCards")
        let redCardsOrdinal = dataReader.GetOrdinal("fRedCards")
        let ownGoalsOrdinal = dataReader.GetOrdinal("fOwnGoals")
        
        {
            Id = dataReader.GetInt32(idOrdinal);
            FixtureId = dataReader.GetInt32(fixtureIdOrdinal);
            PlayerId = dataReader.GetInt32(playerIdOrdinal);
            TotalPoints = dataReader.GetInt32(totalPointsOrdinal);
            MinutesPlayed = dataReader.GetInt32(minutesPlayedOrdinal);
            GoalsScored = dataReader.GetInt32(goalsScoredOrdinal);
            Assists = dataReader.GetInt32(assistsOrdinal);
            CleanSheet = dataReader.GetBoolean(cleanSheetOrdinal);
            ShotsSaved = dataReader.GetInt32(shotsSavedOrdinal);
            PenaltiesSaved = dataReader.GetInt32(penaltiesSavedOrdinal);
            PenaltiesMissed = dataReader.GetInt32(penaltiesMissedOrdinal);
            GoalsConceded = dataReader.GetInt32(goalsConcededOrdinal);
            YellowCards = dataReader.GetInt32(yellowCardsOrdinal);
            RedCard = dataReader.GetInt32(redCardsOrdinal);
            OwnGoals = dataReader.GetInt32(ownGoalsOrdinal);
        }



    let mapToSqlType (players:seq<T>) = 
        let metaData = [|
            new SqlMetaData("fId", SqlDbType.Int);
            new SqlMetaData("FixtureId", SqlDbType.Int);
            new SqlMetaData("PlayerId", SqlDbType.Int);
            new SqlMetaData("TotalPoints", SqlDbType.Int);
            new SqlMetaData("MinutesPlayed", SqlDbType.Int);
            new SqlMetaData("GoalsScored", SqlDbType.Int);
            new SqlMetaData("Assists", SqlDbType.Int);
            new SqlMetaData("CleanSheet", SqlDbType.Bit);
            new SqlMetaData("ShotsSaved", SqlDbType.Int);
            new SqlMetaData("PenaltiesSaved", SqlDbType.Int);
            new SqlMetaData("PenaltiesMissed", SqlDbType.Int);
            new SqlMetaData("GoalsConceded", SqlDbType.Int);
            new SqlMetaData("YellowCards", SqlDbType.Int);
            new SqlMetaData("RedCards", SqlDbType.Int);
            new SqlMetaData("OwnGoals", SqlDbType.Int);
        |]

        let record = new SqlDataRecord(metaData)
        players |> Seq.map (fun player ->
                record.SetInt32(0, player.Id)
                record.SetInt32(1, player.FixtureId)
                record.SetInt32(2, player.PlayerId)
                record.SetInt32(3, player.TotalPoints)
                record.SetInt32(4, player.MinutesPlayed)
                record.SetInt32(5, player.GoalsScored)
                record.SetInt32(6, player.Assists)
                record.SetBoolean(7, player.CleanSheet)
                record.SetInt32(8, player.ShotsSaved)
                record.SetInt32(9, player.PenaltiesSaved)
                record.SetInt32(10, player.PenaltiesMissed)
                record.SetInt32(11, player.GoalsConceded)
                record.SetInt32(12, player.YellowCards)
                record.SetInt32(13, player.RedCard)
                record.SetInt32(14, player.OwnGoals)
                record)


    let getEvents (incidentEvents:seq<IncidentEvent>) = 
        [ "Goal"; "Pass"; "SubstitutionOn"; "SubstitutionOff"; "Card" ]
        |> Seq.map (fun d -> let res = incidentEvents 
                                        |> Seq.filter (fun e -> e.Type.DisplayName = d)
                             (d, res))

    let mapPlayerData fixtureId (ownIncidentEvents:IDictionary<string,seq<IncidentEvent>>) (opponentIncidentEvents:IDictionary<string,seq<IncidentEvent>>) 
                      playerId (playerData:PlayerData) =
        let getRed (incidentEvent:IncidentEvent) = incidentEvent.Qualifiers |> Seq.tryFind (fun x -> x.Type.DisplayName = "Red")
        let getYellow (incidentEvent:IncidentEvent) = incidentEvent.Qualifiers |> Seq.tryFind (fun x -> x.Type.DisplayName = "Yellow")
        let minutesPlayed = 
            let substitutedOn = ownIncidentEvents.Item("SubstitutionOn") |> Seq.tryFind(fun x -> x.PlayerId = playerData.PlayerId)
            let substitutedOff = ownIncidentEvents.Item("SubstitutionOff") |> Seq.tryFind(fun x -> x.PlayerId = playerData.PlayerId)
            match substitutedOn with
            | Some x -> 90 - x.Minute
            | None -> match substitutedOff with
                      | Some x -> x.Minute
                      | None -> if playerData.IsFirstEleven then 90 else 0

        let goalsScored = ownIncidentEvents.Item("Goal") |> Seq.filter(fun x -> x.PlayerId = playerData.PlayerId) |> Seq.length
        let assists = ownIncidentEvents.Item("Pass") |> Seq.filter(fun x -> x.PlayerId = playerData.PlayerId) |> Seq.length
        let goalsConceded = opponentIncidentEvents.Item("Goal") |> Seq.filter (fun x -> x.Minute < minutesPlayed) |> Seq.length
        let cleanSheet = goalsConceded = 0
        let shotsSaved = if (isNull (box playerData.Stats.TotalSaves)) then 0 else playerData.Stats.TotalSaves |> Map.toSeq |> Seq.sumBy (fun (_,v) -> v) |> int
        let yellowCards = ownIncidentEvents.Item("Card") |> Seq.filter(fun x -> x.PlayerId = playerData.PlayerId && (getYellow x).IsSome) |> Seq.length
        let redCards = ownIncidentEvents.Item("Card") |> Seq.filter(fun x -> x.PlayerId = playerData.PlayerId && (getRed x).IsSome) |> Seq.length
                          
        {
            Id = -1;
            FixtureId = fixtureId;
            PlayerId = playerId;
            TotalPoints = -1;
            MinutesPlayed = minutesPlayed;
            GoalsScored = goalsScored;
            Assists = assists;
            CleanSheet = cleanSheet;
            ShotsSaved = shotsSaved;
            PenaltiesSaved = 0;
            PenaltiesMissed = 0;
            GoalsConceded = goalsConceded;
            YellowCards = yellowCards;
            RedCard = redCards;
            OwnGoals = 0;
        }



    let calculateTotalPoints (player:T) (playerPosition:Position) = 
        let minutePoints = 
            match player.MinutesPlayed with
            | 0 -> 0
            | _ when player.MinutesPlayed < 60 -> 1
            | _ -> 2

        let goalPoints = 
            match playerPosition with
            | Position.Goalkeeper | Position.Defender -> 6 * player.GoalsScored
            | Position.Midfielder -> 5 * player.GoalsScored
            | Position.Forward -> 4 * player.GoalsScored
            | _ -> 0

        let assistPoints = 
            3 * player.Assists

        let cleanSheetPoints = 
            let cleanSheet = if player.CleanSheet then 1 else 0
            match playerPosition with
            | Position.Goalkeeper | Position.Defender -> 4 * cleanSheet
            | Position.Midfielder -> 1 * cleanSheet
            | Position.Forward -> 0
            | _ -> 0

        let savesPoints = 
            match playerPosition with
            | Position.Goalkeeper -> player.ShotsSaved / 3 
            | _ -> 0

        let penaltySavePoints = 
            player.PenaltiesSaved * 5

        let penaltyMissedPoints = 
            player.PenaltiesMissed * (-2)

        let goalsConcededPoints = 
            match playerPosition with
            | Position.Goalkeeper | Position.Defender -> (-1) * (player.GoalsConceded / 2)
            | _ -> 0

        let yellowCardPoints = 
            player.YellowCards * -1

        let redCardPoints = 
            player.RedCard * -3

        let ownGoalPoints = 
            player.OwnGoals * -2

        let result = minutePoints + goalPoints + assistPoints + cleanSheetPoints + savesPoints + 
                     penaltySavePoints + penaltyMissedPoints + goalsConcededPoints + yellowCardPoints + 
                     redCardPoints + ownGoalPoints

        result



    let getDataForMatchReport (playerCache:PlayerStaticData.Cache) (fixtureCache:FixtureData.Cache) (report:MatchReport) = async {
        let fixture = fixtureCache.PublicData |> Seq.find (fun x -> x.WhoScoredId = report.WhoScoredId)
        let homeIncidentEvents = report.Home.IncidentEvents |> getEvents |> dict
        let awayIncidentEvents = report.Away.IncidentEvents |> getEvents |> dict

        let homePlayerMapper (player:PlayerData) = 
            let internalPlayer = playerCache.PublicData |> Seq.tryFind (fun x -> x.WhoScoredId = player.PlayerId)
            match internalPlayer with
            | Some x -> Some (mapPlayerData fixture.Id homeIncidentEvents awayIncidentEvents x.Id player)
            | None -> None

        let awayPlayerMapper (player:PlayerData) = 
            let internalPlayer = playerCache.PublicData |> Seq.tryFind (fun x -> x.WhoScoredId = player.PlayerId)
            match internalPlayer with
            | Some x -> Some (mapPlayerData fixture.Id awayIncidentEvents homeIncidentEvents x.Id player)
            | None -> None

        let fantasyPoints player = 
            let internalPlayer = playerCache.PublicData |> Seq.find (fun x -> x.Id = player.PlayerId)
            calculateTotalPoints player internalPlayer.Position

        let map (mapperFunction:PlayerData -> T option) playerSequence = 
            playerSequence
            |> Seq.map (fun x -> mapperFunction x) 
            |> Seq.filter (fun x -> x.IsSome) 
            |> Seq.map (fun x -> x.Value)
            |> Seq.map (fun x -> { x with TotalPoints = fantasyPoints x })
            |> Seq.toArray
        
        let awayPlayers = report.Away.Players |> map awayPlayerMapper
        let homePlayers = report.Home.Players |> map homePlayerMapper
        
        return Array.concat [ homePlayers; awayPlayers ]
    }

