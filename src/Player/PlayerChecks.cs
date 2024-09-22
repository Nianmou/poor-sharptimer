/*
Copyright (C) 2024 Dea Brcka

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.
You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Text.Json;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace SharpTimer
{
    public partial class SharpTimer
    {
        public bool IsAllowedPlayer(CCSPlayerController? player)
        {
            if (player == null || !player.IsValid || player.Pawn == null || !player.PlayerPawn.IsValid || !player.PawnIsAlive || playerTimers[player.Slot].IsNoclip)
            {
                return false;
            }

            int playerSlot = player.Slot;

            CsTeam teamNum = (CsTeam)player.TeamNum;

            bool isAlive = player.PawnIsAlive;
            bool isTeamValid = teamNum == CsTeam.CounterTerrorist || teamNum == CsTeam.Terrorist;

            bool isTeamSpectatorOrNone = teamNum != CsTeam.Spectator && teamNum != CsTeam.None;
            bool isConnected = connectedPlayers.ContainsKey(playerSlot) && playerTimers.ContainsKey(playerSlot);
            bool isConnectedJS = !jumpStatsEnabled || playerJumpStats.ContainsKey(playerSlot);

            return isTeamValid && isTeamSpectatorOrNone && isConnected && isConnectedJS && isAlive;
        }

        private bool IsAllowedSpectator(CCSPlayerController? player)
        {
            if (player == null || !player.IsValid || player.IsBot)
            {
                return false;
            }

            CsTeam teamNum = (CsTeam)player.TeamNum;
            bool isTeamValid = teamNum == CsTeam.Spectator;
            bool isConnected = connectedPlayers.ContainsKey(player.Slot) && playerTimers.ContainsKey(player.Slot);
            bool isObservingValid = player.Pawn?.Value!.ObserverServices?.ObserverTarget != null &&
                                     specTargets.ContainsKey(player.Pawn.Value.ObserverServices.ObserverTarget.Index);

            return isTeamValid && isConnected && isObservingValid;
        }

        async Task IsPlayerATester(string steamId64, int playerSlot)
        {
            try
            {
                string response = await httpClient.GetStringAsync(testerPersonalGifsSource);

                using (JsonDocument jsonDocument = JsonDocument.Parse(response))
                {
                    if (playerTimers.TryGetValue(playerSlot, out PlayerTimerInfo? playerTimer))
                    {
                        playerTimer.IsTester = jsonDocument.RootElement.TryGetProperty(steamId64, out JsonElement steamData);

                        if (playerTimer.IsTester)
                        {
                            if (steamData.TryGetProperty("SmolGif", out JsonElement smolGifElement))
                            {
                                playerTimer.TesterSmolGif = smolGifElement.GetString() ?? "";
                            }

                            if (steamData.TryGetProperty("BigGif", out JsonElement bigGifElement))
                            {
                                playerTimer.TesterBigGif = bigGifElement.GetString() ?? "";
                            }
                        }
                    }
                    else
                    {
                        SharpTimerError($"Error in IsPlayerATester: player not on server anymore");
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error in IsPlayerATester: {ex.Message}");
            }
        }

        async Task<string> GetTesterBigGif(string steamId64)
        {
            try
            {
                string response = await httpClient.GetStringAsync(testerPersonalGifsSource);

                using (JsonDocument jsonDocument = JsonDocument.Parse(response))
                {
                    jsonDocument.RootElement.TryGetProperty(steamId64, out JsonElement steamData);

                    if (steamData.TryGetProperty("BigGif", out JsonElement bigGifElement))
                        return bigGifElement.GetString() ?? "";
                    else
                        return "";
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error in GetTesterBigGif: {ex.Message}");
                return "";
            }
        }

        async Task<string> GetTesterSmolGif(string steamId64)
        {
            try
            {
                string response = await httpClient.GetStringAsync(testerPersonalGifsSource);

                using (JsonDocument jsonDocument = JsonDocument.Parse(response))
                {
                    jsonDocument.RootElement.TryGetProperty(steamId64, out JsonElement steamData);

                    if (steamData.TryGetProperty("SmolGif", out JsonElement smolGifElement))
                        return smolGifElement.GetString() ?? "";
                    else
                        return "";
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error in GetTesterSmolGif: {ex.Message}");
                return "";
            }
        }

        async Task<bool> IsSteamIDaTester(string steamId64)
        {
            try
            {
                string response = await httpClient.GetStringAsync(testerPersonalGifsSource);

                using (JsonDocument jsonDocument = JsonDocument.Parse(response))
                {
                    if (jsonDocument.RootElement.TryGetProperty(steamId64, out JsonElement isTester))
                        return true;
                    else
                        return false;
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Steam IDa 测试器出现错误: {ex.Message}");
                return false;
            }
        }

        private void CheckPlayerCoords(CCSPlayerController? player, Vector playerSpeed)
        {
            try
            {
                // 如果玩家为空或者玩家不被允许，则直接返回  
                if (player == null || !IsAllowedPlayer(player))
                {
                    return;
                }

                // 定义一个错误的向量（0,0,0），用于后续比较  
                Vector incorrectVector = new(0, 0, 0);

                // 尝试获取玩家的位置，如果获取失败则为null  
                Vector? playerPos = player.Pawn?.Value!.CBodyComponent?.SceneNode!.AbsOrigin;

                // 初始化玩家是否在起始区域和结束区域的标志  
                bool isInsideStartBox = false;
                bool isInsideEndBox = false;

                // 检查玩家位置是否为空，或者地图的起始和结束坐标是否有任何一个为错误的向量  
                if (playerPos == null || currentMapStartC1 == incorrectVector || currentMapStartC2 == incorrectVector ||
                    currentMapEndC1 == incorrectVector || currentMapEndC2 == incorrectVector)
                {
                    return;
                }

                // 如果不使用触发器和虚假区域，则检查玩家是否在起始和结束区域内  
                if (!useTriggersAndFakeZones)
                {
                    isInsideStartBox = IsVectorInsideBox(playerPos, currentMapStartC1, currentMapStartC2);
                    isInsideEndBox = IsVectorInsideBox(playerPos, currentMapEndC1, currentMapEndC2);
                }

                // 初始化玩家是否在各个奖励起始区域和结束区域的数组  
                bool[] isInsideBonusStartBox = new bool[11];
                bool[] isInsideBonusEndBox = new bool[11];

                // 遍历所有奖励，检查玩家是否在奖励的起始和结束区域内  
                foreach (int bonus in totalBonuses)
                {
                    if (bonus == 0)
                    {
                        // 如果奖励为0，则跳过（可能是占位符或无效奖励）  
                    }
                    else
                    {
                        // 检查奖励的起始和结束坐标数组是否有效  
                        if (currentBonusStartC1 == null || currentBonusStartC1.Length <= bonus ||
                            currentBonusStartC2 == null || currentBonusStartC2.Length <= bonus ||
                            currentBonusEndC1 == null || currentBonusEndC1.Length <= bonus ||
                            currentBonusEndC2 == null || currentBonusEndC2.Length <= bonus)
                        {
                            // 如果坐标无效，则输出错误信息  
                            SharpTimerError($"奖金坐标无效 {bonus}");
                        }
                        else
                        {
                            // 检查玩家是否在奖励的起始和结束区域内  
                            isInsideBonusStartBox[bonus] = IsVectorInsideBox(playerPos, currentBonusStartC1[bonus], currentBonusStartC2[bonus]);
                            isInsideBonusEndBox[bonus] = IsVectorInsideBox(playerPos, currentBonusEndC1[bonus], currentBonusEndC2[bonus]);
                        }
                    }
                }

                // 如果不使用触发器和虚假区域，则根据玩家在起始和结束区域的情况进行处理  
                if (!useTriggersAndFakeZones)
                {
                    // 如果玩家不在起始区域但在结束区域，则停止计时和录制（如果有）  
                    if (!isInsideStartBox && isInsideEndBox)
                    {
                        OnTimerStop(player);
                        if (enableReplays) OnRecordingStop(player);
                    }
                    // 如果玩家在起始区域，则开始计时和录制（如果有），并检查玩家速度是否超过最大起始速度  
                    else if (isInsideStartBox)
                    {
                        if (playerTimers.TryGetValue(player.Slot, out PlayerTimerInfo? playerTimer))
                        {
                            playerTimer.inStartzone = true;
                        }

                        OnTimerStart(player);
                        if (enableReplays) OnRecordingStart(player);

                        // 检查玩家速度是否超过最大起始速度，并根据设置调整速度  
                        if ((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(playerSpeed.Length()) > maxStartingSpeed) ||
                            (maxStartingSpeedEnabled == true && use2DSpeed == true && Math.Round(playerSpeed.Length2D()) > maxStartingSpeed))
                        {
                            Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                            adjustVelocity(player, maxStartingSpeed, true);
                        }
                    }
                    // 如果玩家不在起始区域，则更新玩家计时器状态  
                    else if (!isInsideStartBox && playerTimers.TryGetValue(player.Slot, out PlayerTimerInfo? playerTimer))
                    {
                        playerTimer.inStartzone = false;
                    }
                }

                // 遍历所有奖励，根据玩家在奖励起始和结束区域的情况进行处理  
                foreach (int bonus in totalBonuses)
                {
                    if (bonus == 0)
                    {
                        // 如果奖励为0，则跳过  
                    }
                    else
                    {
                        // 检查奖励的坐标数组是否有效（与之前重复，可能是为了代码清晰性而保留）  
                        if (currentBonusStartC1 == null || currentBonusStartC1.Length <= bonus ||
                            currentBonusStartC2 == null || currentBonusStartC2.Length <= bonus ||
                            currentBonusEndC1 == null || currentBonusEndC1.Length <= bonus ||
                            currentBonusEndC2 == null || currentBonusEndC2.Length <= bonus)
                        {
                            SharpTimerError($"奖金坐标无效 {bonus}");
                        }
                        else
                        {
                            // 如果玩家不在奖励起始区域但在奖励结束区域，则停止奖励计时和录制（如果有）  
                            if (!isInsideBonusStartBox[bonus] && isInsideBonusEndBox[bonus])
                            {
                                OnBonusTimerStop(player, bonus);
                                if (enableReplays) OnRecordingStop(player);
                            }
                            // 如果玩家在奖励起始区域，则开始奖励计时和录制（如果有），并检查玩家速度是否超过最大起始速度  
                            else if (isInsideBonusStartBox[bonus])
                            {
                                if (playerTimers.TryGetValue(player.Slot, out PlayerTimerInfo? playerTimer))
                                {
                                    playerTimer.inStartzone = true;
                                }

                                OnTimerStart(player, bonus);
                                if (enableReplays) OnRecordingStart(player, bonus);

                                // 检查玩家速度是否超过最大起始速度，并根据设置调整速度（与之前重复）  
                                if ((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(playerSpeed.Length()) > maxStartingSpeed) ||
                                    (maxStartingSpeedEnabled == true && use2DSpeed == true && Math.Round(playerSpeed.Length2D()) > maxStartingSpeed))
                                {
                                    Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                                    adjustVelocity(player, maxStartingSpeed, true);
                                }
                            }
                            // 如果玩家不在奖励起始区域，则更新玩家计时器状态（与之前逻辑部分重复，但针对奖励区域）  
                            else if (!isInsideBonusStartBox[bonus])
                            {
                                if (playerTimers.TryGetValue(player.Slot, out PlayerTimerInfo? playerTimer))
                                {
                                    playerTimer.inStartzone = false; // 注意：这里可能需要根据实际逻辑调整，因为inStartzone可能应该针对具体的奖励区域  
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 捕获异常并输出错误信息  
                SharpTimerError($"检查玩家坐标时出错: {ex.Message}");
            }
        }

        //bot 推动
        private void CheckPlayerTriggerPushCoords(CCSPlayerController player, Vector playerSpeed)
        {
            try
            {
                // 如果玩家为空、玩家不被允许或者触发推送数据为空，则直接返回  
                if (player == null || !IsAllowedPlayer(player) || triggerPushData.Count == 0) return;

                // 获取玩家的当前位置，如果玩家没有实体或者实体没有物理组件或者场景节点，则返回null  
                Vector? playerPos = player.Pawn?.Value!.CBodyComponent?.SceneNode!.AbsOrigin;

                // 如果玩家位置为空，则直接返回  
                if (playerPos == null) return;

                // 根据玩家位置获取触发推送数据  
                var data = GetTriggerPushDataForVector(playerPos);

                // 如果获取到了有效的触发推送数据  
                if (data != null)
                {
                    // 解构数据，获取推送方向和推送速度  
                    var (pushDirEntitySpace, pushSpeed) = data.Value;

                    // 计算玩家当前速度的长度（即速度的大小）  
                    float currentSpeed = playerSpeed.Length();

                    // 计算推送速度与当前速度的差异  
                    float speedDifference = pushSpeed - currentSpeed;

                    // 如果推送速度大于当前速度  
                    if (speedDifference > 0)
                    {
                        // 计算速度变化量  
                        float velocityChange = speedDifference;

                        // 根据推送方向调整玩家的速度  
                        player.PlayerPawn.Value!.AbsVelocity.X += pushDirEntitySpace.X * velocityChange;
                        player.PlayerPawn.Value!.AbsVelocity.Y += pushDirEntitySpace.Y * velocityChange;
                        player.PlayerPawn.Value!.AbsVelocity.Z += pushDirEntitySpace.Z * velocityChange;

                        // 输出调试信息，说明玩家的速度已被调整  
                        SharpTimerDebug($"扳机推力修复：玩家速度已调整 {player.PlayerName} by {speedDifference}");
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果在执行过程中发生异常，输出错误信息  
                SharpTimerError($"检查玩家触发推送坐标时出错: {ex.Message}");
            }
        }
    }
}