using System;
using System.Threading;

namespace MAK_EOL.Classes.PLC
{
    /// <summary>
    /// SLMP_PLC 사용 예제 — DevSimulator와 핸드쉐이크 연동
    ///
    /// [사용법]
    /// PLC_IManager에서 PLC_TYPE에 SLMP 추가 후,
    /// Mtbs_MXCom_PLC 대신 SLMP_PLC를 사용하면 됩니다.
    ///
    /// 기존 코드:
    ///   BasePLC plc = new Mtbs_MXCom_PLC(1, "");
    ///
    /// 변경 코드:
    ///   BasePLC plc = new SLMP_PLC("127.0.0.1", 5000);
    ///
    /// 나머지 Read/Write 호출은 동일합니다.
    /// </summary>
    public class SLMP_PLC_UsageExample
    {
        public static void Main_Example()
        {
            // ============================================
            // 1. 연결
            // ============================================
            BasePLC plc = new SLMP_PLC("127.0.0.1", 5000);
            int ret = plc.DeviceOpen();

            if (ret != 0 || !plc.IsPLCConnect)
            {
                Console.WriteLine("PLC 연결 실패");
                return;
            }
            Console.WriteLine("PLC 연결 성공");

            // ============================================
            // 2. 단일 워드 읽기/쓰기 (기존 코드와 동일)
            // ============================================
            short readVal = 0;
            plc.ReadDevice_Short("100", ref readVal, "D");   // D100 읽기
            Console.WriteLine("D100 = " + readVal);

            plc.WriteDevice_Short("200", 42, "D");           // D200 = 42 쓰기
            Console.WriteLine("D200에 42 쓰기 완료");

            // ============================================
            // 3. 비트 디바이스 읽기/쓰기
            // ============================================
            plc.Set_Bit("100", true, "M");                   // M100 = ON
            Console.WriteLine("M100 ON");

            bool bitState = false;
            plc.Get_Bit("101", ref bitState, "M");           // M101 읽기
            Console.WriteLine("M101 = " + bitState);

            // ============================================
            // 4. 블록 읽기/쓰기 (연속 영역)
            // ============================================
            short[] blockData = null;
            plc.ReadDeviceBlocks_Short("100", 10, ref blockData, "D");  // D100~D109
            Console.WriteLine("D100~D109 블록 읽기 완료");

            short[] writeData = new short[] { 1, 2, 3, 4, 5 };
            plc.WriteDeviceBlocks_Short("200", writeData, "D");         // D200~D204
            Console.WriteLine("D200~D204 블록 쓰기 완료");

            // ============================================
            // 5. 더블 워드 (32bit) 읽기/쓰기
            // ============================================
            plc.SetDoubleDevice("300", 100000, "D");         // D300-D301에 100000 쓰기
            int dwordVal = plc.GetDoubleDevice("300", "D");  // D300-D301 읽기
            Console.WriteLine("D300 (DWord) = " + dwordVal);

            // ============================================
            // 6. 핸드쉐이크 예제 (DevSimulator scenario_testclient.json 연동)
            // ============================================
            Console.WriteLine("\n--- 핸드쉐이크 시작 ---");

            // 요청 신호 ON
            plc.Set_Bit("100", true, "M");                   // M100 = 1
            Console.WriteLine("M100 = ON (요청)");

            // 완료 신호 대기 (M101 == 1)
            Console.WriteLine("M101 대기 중...");
            DateTime timeout = DateTime.Now.AddSeconds(12);
            while (DateTime.Now < timeout)
            {
                bool m101 = false;
                plc.Get_Bit("101", ref m101, "M");
                if (m101)
                {
                    Console.WriteLine("M101 = ON (완료 수신)");
                    break;
                }
                Thread.Sleep(200);
            }

            // 결과 읽기
            short d200 = 0, d201 = 0;
            plc.ReadDevice_Short("200", ref d200, "D");
            plc.ReadDevice_Short("201", ref d201, "D");
            Console.WriteLine("D200 = " + d200 + " (결과 코드)");
            Console.WriteLine("D201 = " + d201 + " (결과 데이터)");

            // 리셋
            plc.Set_Bit("100", false, "M");                  // M100 = 0
            Console.WriteLine("M100 = OFF (리셋)");

            // M101 리셋 대기
            timeout = DateTime.Now.AddSeconds(6);
            while (DateTime.Now < timeout)
            {
                bool m101 = false;
                plc.Get_Bit("101", ref m101, "M");
                if (!m101)
                {
                    Console.WriteLine("M101 = OFF (리셋 완료)");
                    break;
                }
                Thread.Sleep(200);
            }

            Console.WriteLine("--- 핸드쉐이크 완료 ---");

            // ============================================
            // 7. Alive 체크 예제 (주기 태스크 연동)
            // ============================================
            // DevSimulator에서 주기 태스크로 D10을 Toggle 설정(1000ms) 시:
            short prev = -1;
            for (int i = 0; i < 5; i++)
            {
                short alive = 0;
                plc.ReadDevice_Short("10", ref alive, "D");
                if (alive != prev)
                {
                    Console.WriteLine("Alive D10 = " + alive + " (변화 감지)");
                    prev = alive;
                }
                Thread.Sleep(500);
            }

            // ============================================
            // 8. 연결 해제
            // ============================================
            plc.DeviceClose();
            Console.WriteLine("PLC 연결 해제");
        }
    }
}
