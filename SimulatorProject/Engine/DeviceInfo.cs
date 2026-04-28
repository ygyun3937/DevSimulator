namespace SimulatorProject.Engine;

public static class DeviceInfo
{
    public static (string Type, string Description) GetDeviceType(string deviceKey)
    {
        if (string.IsNullOrWhiteSpace(deviceKey))
            return ("", "");

        var prefix = char.ToUpper(deviceKey[0]);
        return prefix switch
        {
            'M' => ("BIT", "내부 릴레이"),
            'X' => ("BIT", "입력"),
            'Y' => ("BIT", "출력"),
            'B' => ("BIT", "링크 릴레이"),
            'L' => ("BIT", "래치 릴레이"),
            'D' => ("WORD", "데이터 레지스터"),
            'W' => ("WORD", "링크 레지스터"),
            'R' => ("WORD", "파일 레지스터"),
            'Z' => ("WORD", "인덱스 레지스터"),
            _   => ("?", "알 수 없는 디바이스")
        };
    }

    public static string GetLabel(string deviceKey)
    {
        var (type, desc) = GetDeviceType(deviceKey);
        if (string.IsNullOrEmpty(type)) return "";
        return $"{type} | {desc}";
    }
}
