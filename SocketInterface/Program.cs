using System;
using System.Threading;
using Server;
using Client;
using System.Collections.Generic;

namespace SocketInterface
{
    class Program
    {
        static void Main()
        {
            //데이터 수신 서버 가동 예시
            MyServer.initialize("210.181.148.33", 10042);
            MyServer.SetFileDirectory(@"D:\rcv");
            MyServer.ServerStart();
            while (true)
            {
                Thread.Sleep(50);
            }
            MyServer.ServerStop();

            //클라이언트 메시지 전송 예시
            MyClient.initialize("210.181.148.33", 10042);
            while (true)
            {
                Console.WriteLine("텍스트를 입력 해 주세요");
                string text = Console.ReadLine();
                MyClient.SendData(text, null);
                Thread.Sleep(50);
            }


            //클라이언트 파일 전송 예시
            MyClient.initialize("210.181.148.33", 10042);
            while (true)
            {
                Console.WriteLine("텍스트를 입력 하면 파일이 전송 됩니다");
                string text = Console.ReadLine();

                List<string> fileFulleNameList = new List<string>();
                fileFulleNameList.Add(@"D:\TestTextFile.txt");
                fileFulleNameList.Add(@"D:\TestTextFile__a.txt");
                fileFulleNameList.Add(@"D:\Test.txt");

                MyClient.SendData("", fileFulleNameList);
                Thread.Sleep(50);
            }
        }
    }
}
