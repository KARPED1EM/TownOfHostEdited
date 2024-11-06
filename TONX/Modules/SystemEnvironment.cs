using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TONX.Modules;

public static class SystemEnvironment
{
    public static void SetEnvironmentVariables()
    {
        // ユーザ環境変数に最近開かれたTOHアモアスフォルダのパスを設定
        Environment.SetEnvironmentVariable("TOWN_OF_NEXT_DIR_ROOT", Environment.CurrentDirectory, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable("TOWN_OF_NEXT_DIR_LOGS", Utils.GetLogFolder().FullName, EnvironmentVariableTarget.User);
    }
}
