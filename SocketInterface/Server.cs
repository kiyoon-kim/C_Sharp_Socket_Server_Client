using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using myJSON;
using System.Threading;

namespace Server
{
    public static class MyServer
    {
        private static bool isRunning = false;
        private static string serverAddress = "";
        private static int serverPortNum = 0;
        private static string fileDirectoryPath = "";
        private static Socket sock;

        public static void SetFileDirectory(string directoryPath)
        {
            fileDirectoryPath = directoryPath;
            if(fileDirectoryPath.Substring(directoryPath.Length - 1, 1) != @"\") fileDirectoryPath += @"\";
            string aaa = fileDirectoryPath;
        }
        public static void initialize(string serverIP, int serverPort)
        {
            serverAddress = serverIP;
            serverPortNum = serverPort;
            sock = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp
                    );
        }

        public static void ServerStop()
        {
            isRunning = false;
        }

        public static void ServerStart()
        {
            IPAddress addr = IPAddress.Parse(serverAddress);
            IPEndPoint iep = new IPEndPoint(addr, serverPortNum);
            sock.Bind(iep);
            isRunning = true;

            //백로그 큐 크기 설정
            sock.Listen(5);

            //사용자 접속
            Socket client;
            while (isRunning)
            {
                //Client 접속 대기
                client = sock.Accept();

                //Client 접속 완료
                Thread acceptClient = new Thread(() =>
                {
                    GetMessage(client);
                });
                acceptClient.Start();
            }

            //소켓 닫기
            sock.Close();
        }

        private static void GetMessage(Socket client)
        {
            //패킷 사이즈 설정
            int packetSize = 128;
            byte[] byteData_FirstMsg = new byte[packetSize];

            //Client 끝점 정보 (필요시 로그 기록 하기 위함)
            IPEndPoint iep = client.RemoteEndPoint as IPEndPoint;

            //첫번째 메시지를 받는다 (메시지는 데이터 타입과 바이트 크기 정보를 갖고 있다)
            client.Receive(byteData_FirstMsg);
            MemoryStream ms = new MemoryStream(byteData_FirstMsg);
            BinaryReader br = new BinaryReader(ms);
            string msg = br.ReadString();
            br.Close();
            ms.Close();
            Console.WriteLine("{0}:{1} → {2}", iep.Address, iep.Port, msg);

            //첫번째로 받은 메시지를 JSON Parse하여 데이터 타입을 확인 한다
            toolJSON tempJSON = new toolJSON();
            string dataType = tempJSON.GetData_FromJSON(msg, JsonItems.DataType);

            //데이터 타입이 String일 경우 
            if (dataType == DataTypes.String)
            {
                //첫번째로 받은 메시지를 JSON Parse하여 데이터 크기를 확인 한다
                int byteDataSize_Total = Convert.ToInt32(tempJSON.GetData_FromJSON(msg, JsonItems.DataLength));
                int byteDataSize_Split = Convert.ToInt32(tempJSON.GetData_FromJSON(msg, JsonItems.StringLength));

                //두번째로 String 본문을 받을 수 있도록 Byte 배열을 선언 한다
                //패킷 사이즈 단위로 데이터를 수신 한다
                byte[] byteData_SecondMsg = new byte[byteDataSize_Total];
                for (int i = 0; i < byteDataSize_Total; i += packetSize)
                {
                    byte[] devidedByte = new byte[packetSize];
                    client.Receive(devidedByte);
                    Array.Copy(devidedByte, 0, byteData_SecondMsg, i, packetSize);
                }

                byte[] byteData_SplitedSecondMsg = new byte[byteDataSize_Split];
                Array.Copy(byteData_SecondMsg, 0, byteData_SplitedSecondMsg, 0, byteDataSize_Split);

                //Encoding 정보 입력 (.NET Core의 경우 한글을 기본제공 하지 않기 때문에 별도 입력 하였음)
                var endcoingCode = 949;
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                System.Text.Encoding ks_c_5601 = System.Text.Encoding.GetEncoding(endcoingCode);

                //Encoding하여 메시지 취득
                string message = ks_c_5601.GetString(byteData_SplitedSecondMsg);

                //수신 한 메시지 표현
                Console.WriteLine("{0}:{1} → {2}", iep.Address, iep.Port, message);


                string additinalDataType = tempJSON.GetData_FromJSON(message, JsonItems.DataType);
                if (additinalDataType == DataTypes.File)
                {
                    //데이터 크기를 확인 한다
                    int byteDataSize_Files = Convert.ToInt32(tempJSON.GetData_FromJSON(message, JsonItems.DataLength));

                    //파일들의 이름과 크기를 확인 한다
                    string fileSizeList = tempJSON.GetData_FromJSON(message, JsonItems.FileSize);
                    string fileNameList = tempJSON.GetData_FromJSON(message, JsonItems.FileName);
                    string[] fileSize_Devided = fileSizeList.Split(",");
                    string[] fileName_Devided = fileNameList.Split(",");

                    //패킷 사이즈 단위로 데이터를 수신 한다
                    byte[] byteData_File = new byte[byteDataSize_Files];
                    for (int i = 0; i < byteDataSize_Files; i += packetSize)
                    {
                        byte[] devidedByte = new byte[packetSize];
                        client.Receive(devidedByte);
                        Array.Copy(devidedByte, 0, byteData_File, i, packetSize);
                    }

                    //파일을 저장 한다
                    int savedFileCount = 0;
                    int index_FileByte = 0;
                    string directoryPath = fileDirectoryPath;
                    while (savedFileCount != fileSize_Devided.Length)
                    {
                        //바이트 데이터 입력
                        int tempFileSize = Convert.ToInt32(fileSize_Devided[savedFileCount]);
                        byte[] tempFileBytes = new byte[tempFileSize];
                        Array.Copy(byteData_File, index_FileByte, tempFileBytes, 0, tempFileSize);

                        //파일 저장
                        string tempFileName = directoryPath + fileName_Devided[savedFileCount];
                        FileStream fs = new FileStream(tempFileName, FileMode.Create);
                        fs.Write(tempFileBytes, 0, tempFileBytes.Length);
                        fs.Close();

                        //저장 된 파일 개수 업데이트
                        savedFileCount++;

                        //전체 바이트의 어레이 시작점 업데이트
                        index_FileByte += tempFileSize;
                    }
                }
            }
        }
    }
}