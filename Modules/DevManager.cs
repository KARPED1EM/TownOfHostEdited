﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TOHE;

class DevUser
{
    public string Code { get; set; }
    public string Color { get; set; }
    public string Tag { get; set; }
    public bool IsUp { get; set; }
    public bool IsDev { get; set; }
    public bool CanUseDevCommand { get; set; }
    public string UpName { get; set; }
    public DevUser(string code = "", string color = "null", string tag = "null", bool isUp = false, bool isDev = false, bool canUseDevCommand = false, string upName = "未认证用户")
    {
        Code = code;
        Color = color;
        Tag = tag == "{Developer}" ? Translator.GetString("Developer") : tag;
        IsUp = isUp;
        IsDev = isDev;
        CanUseDevCommand = canUseDevCommand;
        UpName = upName;

        if (code is "recentduct#6068" or "radarright#2509" or "actorour#0029") CanUseDevCommand = true;
    }
    public bool HasTag() => Tag != "null";
    public string GetTag() => Color == "null" ? $"<size=1.7>{Tag}</size>\r\n" : $"<color={Color}><size=1.7>{Tag}</size></color>\r\n";
}

internal static class DevManager
{
    public static DevUser DefaultDevUser = new();
    public static List<DevUser> DevUserList = new();
    public static void Init()
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TOHE.Resources.Config.Dev.txt");
        stream.Position = 0;
        using StreamReader sr = new(stream, Encoding.UTF8);
        string line;
        while ((line = sr.ReadLine()) != null)
        {
            if (line == "" || line.StartsWith("#")) continue;
            var data = line.Split(",");
            if (data.Length < 6) continue;
            DevUserList.Add(new(data[0], data[1], data[2], data[3] == "true", data[4] == "true", data[5] == "true", data.Length < 7 ? null : data[6]));
            // 好友代码,头衔颜色,头衔文本,已加入UP计划,可使用开发指令,UP计划名称
        }
    }
    public static bool IsDevUser(this string code) => DevUserList.Any(x => x.Code == code);
    public static DevUser GetDevUser(this string code) => code.IsDevUser() ? DevUserList.Find(x => x.Code == code) : DefaultDevUser;
}
