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
                SharpTimerError($"Steam IDa ���������ִ���: {ex.Message}");
                return false;
            }
        }

        private void CheckPlayerCoords(CCSPlayerController? player, Vector playerSpeed)
        {
            try
            {
                // ������Ϊ�ջ�����Ҳ���������ֱ�ӷ���  
                if (player == null || !IsAllowedPlayer(player))
                {
                    return;
                }

                // ����һ�������������0,0,0�������ں����Ƚ�  
                Vector incorrectVector = new(0, 0, 0);

                // ���Ի�ȡ��ҵ�λ�ã������ȡʧ����Ϊnull  
                Vector? playerPos = player.Pawn?.Value!.CBodyComponent?.SceneNode!.AbsOrigin;

                // ��ʼ������Ƿ�����ʼ����ͽ�������ı�־  
                bool isInsideStartBox = false;
                bool isInsideEndBox = false;

                // ������λ���Ƿ�Ϊ�գ����ߵ�ͼ����ʼ�ͽ��������Ƿ����κ�һ��Ϊ���������  
                if (playerPos == null || currentMapStartC1 == incorrectVector || currentMapStartC2 == incorrectVector ||
                    currentMapEndC1 == incorrectVector || currentMapEndC2 == incorrectVector)
                {
                    return;
                }

                // �����ʹ�ô����������������������Ƿ�����ʼ�ͽ���������  
                if (!useTriggersAndFakeZones)
                {
                    isInsideStartBox = IsVectorInsideBox(playerPos, currentMapStartC1, currentMapStartC2);
                    isInsideEndBox = IsVectorInsideBox(playerPos, currentMapEndC1, currentMapEndC2);
                }

                // ��ʼ������Ƿ��ڸ���������ʼ����ͽ������������  
                bool[] isInsideBonusStartBox = new bool[11];
                bool[] isInsideBonusEndBox = new bool[11];

                // �������н������������Ƿ��ڽ�������ʼ�ͽ���������  
                foreach (int bonus in totalBonuses)
                {
                    if (bonus == 0)
                    {
                        // �������Ϊ0����������������ռλ������Ч������  
                    }
                    else
                    {
                        // ��齱������ʼ�ͽ������������Ƿ���Ч  
                        if (currentBonusStartC1 == null || currentBonusStartC1.Length <= bonus ||
                            currentBonusStartC2 == null || currentBonusStartC2.Length <= bonus ||
                            currentBonusEndC1 == null || currentBonusEndC1.Length <= bonus ||
                            currentBonusEndC2 == null || currentBonusEndC2.Length <= bonus)
                        {
                            // ���������Ч�������������Ϣ  
                            SharpTimerError($"����������Ч {bonus}");
                        }
                        else
                        {
                            // �������Ƿ��ڽ�������ʼ�ͽ���������  
                            isInsideBonusStartBox[bonus] = IsVectorInsideBox(playerPos, currentBonusStartC1[bonus], currentBonusStartC2[bonus]);
                            isInsideBonusEndBox[bonus] = IsVectorInsideBox(playerPos, currentBonusEndC1[bonus], currentBonusEndC2[bonus]);
                        }
                    }
                }

                // �����ʹ�ô��������������������������ʼ�ͽ��������������д���  
                if (!useTriggersAndFakeZones)
                {
                    // �����Ҳ�����ʼ�����ڽ���������ֹͣ��ʱ��¼�ƣ�����У�  
                    if (!isInsideStartBox && isInsideEndBox)
                    {
                        OnTimerStop(player);
                        if (enableReplays) OnRecordingStop(player);
                    }
                    // ����������ʼ������ʼ��ʱ��¼�ƣ�����У������������ٶ��Ƿ񳬹������ʼ�ٶ�  
                    else if (isInsideStartBox)
                    {
                        if (playerTimers.TryGetValue(player.Slot, out PlayerTimerInfo? playerTimer))
                        {
                            playerTimer.inStartzone = true;
                        }

                        OnTimerStart(player);
                        if (enableReplays) OnRecordingStart(player);

                        // �������ٶ��Ƿ񳬹������ʼ�ٶȣ����������õ����ٶ�  
                        if ((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(playerSpeed.Length()) > maxStartingSpeed) ||
                            (maxStartingSpeedEnabled == true && use2DSpeed == true && Math.Round(playerSpeed.Length2D()) > maxStartingSpeed))
                        {
                            Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                            adjustVelocity(player, maxStartingSpeed, true);
                        }
                    }
                    // �����Ҳ�����ʼ�����������Ҽ�ʱ��״̬  
                    else if (!isInsideStartBox && playerTimers.TryGetValue(player.Slot, out PlayerTimerInfo? playerTimer))
                    {
                        playerTimer.inStartzone = false;
                    }
                }

                // �������н�������������ڽ�����ʼ�ͽ��������������д���  
                foreach (int bonus in totalBonuses)
                {
                    if (bonus == 0)
                    {
                        // �������Ϊ0��������  
                    }
                    else
                    {
                        // ��齱�������������Ƿ���Ч����֮ǰ�ظ���������Ϊ�˴��������Զ�������  
                        if (currentBonusStartC1 == null || currentBonusStartC1.Length <= bonus ||
                            currentBonusStartC2 == null || currentBonusStartC2.Length <= bonus ||
                            currentBonusEndC1 == null || currentBonusEndC1.Length <= bonus ||
                            currentBonusEndC2 == null || currentBonusEndC2.Length <= bonus)
                        {
                            SharpTimerError($"����������Ч {bonus}");
                        }
                        else
                        {
                            // �����Ҳ��ڽ�����ʼ�����ڽ�������������ֹͣ������ʱ��¼�ƣ�����У�  
                            if (!isInsideBonusStartBox[bonus] && isInsideBonusEndBox[bonus])
                            {
                                OnBonusTimerStop(player, bonus);
                                if (enableReplays) OnRecordingStop(player);
                            }
                            // �������ڽ�����ʼ������ʼ������ʱ��¼�ƣ�����У������������ٶ��Ƿ񳬹������ʼ�ٶ�  
                            else if (isInsideBonusStartBox[bonus])
                            {
                                if (playerTimers.TryGetValue(player.Slot, out PlayerTimerInfo? playerTimer))
                                {
                                    playerTimer.inStartzone = true;
                                }

                                OnTimerStart(player, bonus);
                                if (enableReplays) OnRecordingStart(player, bonus);

                                // �������ٶ��Ƿ񳬹������ʼ�ٶȣ����������õ����ٶȣ���֮ǰ�ظ���  
                                if ((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(playerSpeed.Length()) > maxStartingSpeed) ||
                                    (maxStartingSpeedEnabled == true && use2DSpeed == true && Math.Round(playerSpeed.Length2D()) > maxStartingSpeed))
                                {
                                    Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                                    adjustVelocity(player, maxStartingSpeed, true);
                                }
                            }
                            // �����Ҳ��ڽ�����ʼ�����������Ҽ�ʱ��״̬����֮ǰ�߼������ظ�������Խ�������  
                            else if (!isInsideBonusStartBox[bonus])
                            {
                                if (playerTimers.TryGetValue(player.Slot, out PlayerTimerInfo? playerTimer))
                                {
                                    playerTimer.inStartzone = false; // ע�⣺���������Ҫ����ʵ���߼���������ΪinStartzone����Ӧ����Ծ���Ľ�������  
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // �����쳣�����������Ϣ  
                SharpTimerError($"����������ʱ����: {ex.Message}");
            }
        }

        //bot �ƶ�
        private void CheckPlayerTriggerPushCoords(CCSPlayerController player, Vector playerSpeed)
        {
            try
            {
                // ������Ϊ�ա���Ҳ���������ߴ�����������Ϊ�գ���ֱ�ӷ���  
                if (player == null || !IsAllowedPlayer(player) || triggerPushData.Count == 0) return;

                // ��ȡ��ҵĵ�ǰλ�ã�������û��ʵ�����ʵ��û������������߳����ڵ㣬�򷵻�null  
                Vector? playerPos = player.Pawn?.Value!.CBodyComponent?.SceneNode!.AbsOrigin;

                // ������λ��Ϊ�գ���ֱ�ӷ���  
                if (playerPos == null) return;

                // �������λ�û�ȡ������������  
                var data = GetTriggerPushDataForVector(playerPos);

                // �����ȡ������Ч�Ĵ�����������  
                if (data != null)
                {
                    // �⹹���ݣ���ȡ���ͷ���������ٶ�  
                    var (pushDirEntitySpace, pushSpeed) = data.Value;

                    // ������ҵ�ǰ�ٶȵĳ��ȣ����ٶȵĴ�С��  
                    float currentSpeed = playerSpeed.Length();

                    // ���������ٶ��뵱ǰ�ٶȵĲ���  
                    float speedDifference = pushSpeed - currentSpeed;

                    // ��������ٶȴ��ڵ�ǰ�ٶ�  
                    if (speedDifference > 0)
                    {
                        // �����ٶȱ仯��  
                        float velocityChange = speedDifference;

                        // �������ͷ��������ҵ��ٶ�  
                        player.PlayerPawn.Value!.AbsVelocity.X += pushDirEntitySpace.X * velocityChange;
                        player.PlayerPawn.Value!.AbsVelocity.Y += pushDirEntitySpace.Y * velocityChange;
                        player.PlayerPawn.Value!.AbsVelocity.Z += pushDirEntitySpace.Z * velocityChange;

                        // ���������Ϣ��˵����ҵ��ٶ��ѱ�����  
                        SharpTimerDebug($"��������޸�������ٶ��ѵ��� {player.PlayerName} by {speedDifference}");
                    }
                }
            }
            catch (Exception ex)
            {
                // �����ִ�й����з����쳣�����������Ϣ  
                SharpTimerError($"�����Ҵ�����������ʱ����: {ex.Message}");
            }
        }
    }
}