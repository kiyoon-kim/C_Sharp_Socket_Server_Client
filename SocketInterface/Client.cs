using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using myJSON;
using System.Threading;

namespace Client
{
    public static class MyClient
    {
        private static bool connected = false;
        private static string serverAddress = "";
        private static int serverPortNum = 0;
        private static Socket sock;

        public static void initialize(string serverIP, int serverPort)
        {
            serverAddress = serverIP;
            serverPortNum = serverPort;
        }

        public static string SendData(string message = null, List<string> FileFullnameList = null)
        {
            //데이터 구분
            bool isFileTransfer = false;
            if (FileFullnameList != null) isFileTransfer = true;

            //이전 접속 종료 대기
            if (!WaitConnectionEnds()) return "이전 접속이 종료 되지 않고 있습니다.";

            //TCP 통신 설정
            sock = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp
                    );

            //Server 정보 입력 및 연결
            IPAddress serverAdderss = IPAddress.Parse(serverAddress);
            IPEndPoint iep = new IPEndPoint(serverAdderss, serverPortNum);

            sock.Connect(iep);
            connected = true;

            //패킷 사이즈 및 바이트 배열 설정
            int packetSize = 128;
            byte[] byteData_StringSize; //첫번째로 보내는 메시지 (JSON 형식으로 String의 크기를 전송)
            byte[] byteData_StringValue; //두번째로 보내는 메시지 (String 메시지 본문을 전송)
            byte[] byteData_FileData; //세번째로 보내는 메시지 (파일 정보 전송)

            List<string> fileFulleNameList = FileFullnameList;

            //파일 이름 취합
            List<FileItem> fileItemList = new List<FileItem>(); //파일 이름과 크기 정보
            int sizeOf_Files = 0; //전체 파일 전송 크기
            if(isFileTransfer)
            {
                foreach (string fileFullName in fileFulleNameList)
                {
                    FileInfo tempFileInfo = new FileInfo(fileFullName);

                    FileItem tempFileItem = new FileItem();
                    tempFileItem.fileFullName = tempFileInfo.FullName;
                    tempFileItem.fileName = tempFileInfo.Name;
                    tempFileItem.fileSize_Bytes = Convert.ToInt32(tempFileInfo.Length);
                    sizeOf_Files += tempFileItem.fileSize_Bytes;
                    fileItemList.Add(tempFileItem);

                }
            }
            int byteDataLength_FileData = (sizeOf_Files / packetSize + 1) * packetSize;

            //Encoding 정보 입력 (.NET Core의 경우 한글을 기본제공 하지 않기 때문에 별도 입력 하였음)
            var endcoingCode = 949;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            System.Text.Encoding ks_c_5601 = System.Text.Encoding.GetEncoding(endcoingCode);

            //파일 이름과 크기를 Serialized JSON으로 취득 (이 부분은 별도 코드 구현)
            toolJSON jsonMsgInfo = new toolJSON();
            string msgInfo = "";

            if (isFileTransfer)
            {
                msgInfo = jsonMsgInfo.FileInfo(fileItemList, byteDataLength_FileData);
            }
            else
            {
                msgInfo = message;
            }
            

            //메시지의 바이트 사이즈를 계산
            //패킷 사이즈의 정수 배수로 계산 하며 "ks_c_5601" 인코딩값 기준으로 계산 한다
            byte[] rawByte = ks_c_5601.GetBytes(msgInfo);
            int byteDataLength = ((int)rawByte.Length / packetSize + 1) * packetSize;

            //전송(1) - 메시지의 바이트 크기를 JSON 형식으로 전송 (JSON 변환 부분은 별도 코드 작성 필요)
            //{"DataType":"string","DataLength":"128"}
            string firstMsg = jsonMsgInfo.StringInfo(rawByte.Length, byteDataLength);
            byteData_StringSize = new byte[packetSize];
            MemoryStream ms = new MemoryStream(byteData_StringSize);
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write(firstMsg);
            bw.Close();
            ms.Close();
            sock.Send(byteData_StringSize);

            //전송(2) - 파일 정보를 메시지로 전송 (파일이 많으면 길이가 길어질 수 있으니 패킷 단위로 분할 전송 한다)
            //byteData_StringSize = new byte[packetSize];
            byteData_StringValue = new byte[byteDataLength];
            Array.Copy(rawByte, 0, byteData_StringValue, 0, rawByte.Length);
            for (int i = 0; i < byteDataLength; i += packetSize) //패킷 단위로 분할 하여 데이터를 전송 한다
            {
                byte[] devidedByte = new byte[packetSize];
                Array.Copy(byteData_StringValue, i, devidedByte, 0, packetSize);
                sock.Send(devidedByte);
            }

            if(isFileTransfer)
            {
                //전송(3) - 파일 정보를 Byte형식으로 전송 한다
                byteData_FileData = new byte[byteDataLength_FileData];
                int byteWriteIndex = 0;
                foreach (FileItem item in fileItemList)
                {
                    FileStream fs = File.OpenRead(item.fileFullName);
                    fs.Read(byteData_FileData, byteWriteIndex, item.fileSize_Bytes);
                    byteWriteIndex += item.fileSize_Bytes;
                    fs.Close();
                }


                //Array.Copy(fileBytes, 0, byteData_FileData, 0, fileBytes.Length);
                for (int i = 0; i < byteDataLength_FileData; i += packetSize) //패킷 단위로 분할 하여 데이터를 전송 한다
                {
                    byte[] devidedByte = new byte[packetSize];
                    Array.Copy(byteData_FileData, i, devidedByte, 0, packetSize);
                    sock.Send(devidedByte);
                }
            }

            //소켓을 닫는다
            sock.Close();

            connected = false;

            return "완료";
        }

        //public static string SendMessage(string message)
        //{
        //    //이전 접속 종료 대기
        //    if (!WaitConnectionEnds()) return "이전 접속이 종료 되지 않고 있습니다.";

        //    //TCP 통신 설정
        //    sock = new Socket(
        //            AddressFamily.InterNetwork,
        //            SocketType.Stream,
        //            ProtocolType.Tcp
        //            );

        //    //Server 정보 입력 및 연결
        //    IPAddress serverAdderss = IPAddress.Parse(serverAddress);
        //    IPEndPoint iep = new IPEndPoint(serverAdderss, serverPortNum);

        //    sock.Connect(iep);
        //    connected = true;

        //    //패킷 사이즈 및 바이트 배열 설정
        //    int packetSize = 128;
        //    byte[] byteData_StringSize; //첫번째로 보내는 메시지 (JSON 형식으로 String의 크기를 전송)
        //    byte[] byteData_StringValue; //두번째로 보내는 메시지 (String 메시지 본문을 전송)

        //    //파일 이름 취합
        //    List<FileItem> fileItemList = new List<FileItem>(); //파일 이름과 크기 정보
        //    int sizeOf_Files = 0; //전체 파일 전송 크기
        //    foreach (string fileFullName in fileFulleNameList)
        //    {
        //        FileInfo tempFileInfo = new FileInfo(fileFullName);

        //        FileItem tempFileItem = new FileItem();
        //        tempFileItem.fileFullName = tempFileInfo.FullName;
        //        tempFileItem.fileName = tempFileInfo.Name;
        //        tempFileItem.fileSize_Bytes = Convert.ToInt32(tempFileInfo.Length);
        //        sizeOf_Files += tempFileItem.fileSize_Bytes;
        //        fileItemList.Add(tempFileItem);

        //    }
        //    int byteDataLength_FileData = (sizeOf_Files / packetSize + 1) * packetSize;

        //    //Encoding 정보 입력 (.NET Core의 경우 한글을 기본제공 하지 않기 때문에 별도 입력 하였음)
        //    var endcoingCode = 949;
        //    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        //    System.Text.Encoding ks_c_5601 = System.Text.Encoding.GetEncoding(endcoingCode);

        //    //파일 이름과 크기를 Serialized JSON으로 취득 (이 부분은 별도 코드 구현)
        //    toolJSON jsonMsgInfo = new toolJSON();
        //    string fileInfoSummary = jsonMsgInfo.FileInfo(fileItemList, byteDataLength_FileData);

        //    //메시지의 바이트 사이즈를 계산
        //    //패킷 사이즈의 정수 배수로 계산 하며 "ks_c_5601" 인코딩값 기준으로 계산 한다
        //    byte[] rawByte = ks_c_5601.GetBytes(fileInfoSummary);
        //    int byteDataLength = ((int)rawByte.Length / packetSize + 1) * packetSize;

        //    //전송(1) - 메시지의 바이트 크기를 JSON 형식으로 전송 (JSON 변환 부분은 별도 코드 작성 필요)
        //    //{"DataType":"string","DataLength":"128"}
        //    string firstMsg = jsonMsgInfo.OjectSummary_String(byteDataLength);
        //    byteData_StringSize = new byte[packetSize];
        //    MemoryStream ms = new MemoryStream(byteData_StringSize);
        //    BinaryWriter bw = new BinaryWriter(ms);
        //    bw.Write(firstMsg);
        //    bw.Close();
        //    ms.Close();
        //    sock.Send(byteData_StringSize);

        //    //전송(2) - 파일 정보를 메시지로 전송 (파일이 많으면 길이가 길어질 수 있으니 패킷 단위로 분할 전송 한다)
        //    //byteData_StringSize = new byte[packetSize];
        //    byteData_StringValue = new byte[byteDataLength];
        //    Array.Copy(rawByte, 0, byteData_StringValue, 0, rawByte.Length);
        //    for (int i = 0; i < byteDataLength; i += packetSize) //패킷 단위로 분할 하여 데이터를 전송 한다
        //    {
        //        byte[] devidedByte = new byte[packetSize];
        //        Array.Copy(byteData_StringValue, i, devidedByte, 0, packetSize);
        //        sock.Send(devidedByte);
        //    }

        //    //전송(3) - 파일 정보를 Byte형식으로 전송 한다
        //    byteData_FileData = new byte[byteDataLength_FileData];
        //    int byteWriteIndex = 0;
        //    foreach (FileItem item in fileItemList)
        //    {
        //        FileStream fs = File.OpenRead(item.fileFullName);
        //        fs.Read(byteData_FileData, byteWriteIndex, item.fileSize_Bytes);
        //        byteWriteIndex += item.fileSize_Bytes;
        //        fs.Close();
        //    }


        //    //Array.Copy(fileBytes, 0, byteData_FileData, 0, fileBytes.Length);
        //    for (int i = 0; i < byteDataLength_FileData; i += packetSize) //패킷 단위로 분할 하여 데이터를 전송 한다
        //    {
        //        byte[] devidedByte = new byte[packetSize];
        //        Array.Copy(byteData_FileData, i, devidedByte, 0, packetSize);
        //        sock.Send(devidedByte);
        //    }


        //    //소켓을 닫는다
        //    sock.Close();

        //    connected = false;

        //    return "완료";
        //}

        //public static string SendFiles(List<string> fileFullnameList)
        //{
        //    //이전 접속 종료 대기
        //    if (!WaitConnectionEnds()) return "이전 접속이 종료 되지 않고 있습니다.";

        //    //TCP 통신 설정
        //    sock = new Socket(
        //            AddressFamily.InterNetwork,
        //            SocketType.Stream,
        //            ProtocolType.Tcp
        //            );

        //    //Server 정보 입력 및 연결
        //    IPAddress serverAdderss = IPAddress.Parse(serverAddress);
        //    IPEndPoint iep = new IPEndPoint(serverAdderss, serverPortNum);

        //    sock.Connect(iep);
        //    connected = true;

        //    //패킷 사이즈 및 바이트 배열 설정
        //    int packetSize = 128;
        //    byte[] byteData_StringSize; //첫번째로 보내는 메시지 (JSON 형식으로 String의 크기를 전송)
        //    byte[] byteData_StringValue; //두번째로 보내는 메시지 (String 메시지 본문을 전송)
        //    byte[] byteData_FileData; //세번째로 보내는 메시지 (파일 정보 전송)

        //    //파일 이름 취득
        //    List<string> fileFulleNameList = new List<string>(fileFullnameList);

        //    //파일 이름 취합
        //    List<FileItem> fileItemList = new List<FileItem>(); //파일 이름과 크기 정보
        //    int sizeOf_Files = 0; //전체 파일 전송 크기
        //    foreach (string fileFullName in fileFulleNameList)
        //    {
        //        FileInfo tempFileInfo = new FileInfo(fileFullName);

        //        FileItem tempFileItem = new FileItem();
        //        tempFileItem.fileFullName = tempFileInfo.FullName;
        //        tempFileItem.fileName = tempFileInfo.Name;
        //        tempFileItem.fileSize_Bytes = Convert.ToInt32(tempFileInfo.Length);
        //        sizeOf_Files += tempFileItem.fileSize_Bytes;
        //        fileItemList.Add(tempFileItem);

        //    }
        //    int byteDataLength_FileData = (sizeOf_Files / packetSize + 1) * packetSize;

        //    //Encoding 정보 입력 (.NET Core의 경우 한글을 기본제공 하지 않기 때문에 별도 입력 하였음)
        //    var endcoingCode = 949;
        //    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        //    System.Text.Encoding ks_c_5601 = System.Text.Encoding.GetEncoding(endcoingCode);

        //    //파일 이름과 크기를 Serialized JSON으로 취득 (이 부분은 별도 코드 구현)
        //    toolJSON jsonMsgInfo = new toolJSON();
        //    string fileInfoSummary = jsonMsgInfo.FileInfo(fileItemList, byteDataLength_FileData);

        //    //메시지의 바이트 사이즈를 계산
        //    //패킷 사이즈의 정수 배수로 계산 하며 "ks_c_5601" 인코딩값 기준으로 계산 한다
        //    byte[] rawByte = ks_c_5601.GetBytes(fileInfoSummary);
        //    int byteDataLength = ((int)rawByte.Length / packetSize + 1) * packetSize;

        //    //전송(1) - 메시지의 바이트 크기를 JSON 형식으로 전송 (JSON 변환 부분은 별도 코드 작성 필요)
        //    //{"DataType":"string","DataLength":"128"}
        //    string firstMsg = jsonMsgInfo.OjectSummary_String(byteDataLength);
        //    byteData_StringSize = new byte[packetSize];
        //    MemoryStream ms = new MemoryStream(byteData_StringSize);
        //    BinaryWriter bw = new BinaryWriter(ms);
        //    bw.Write(firstMsg);
        //    bw.Close();
        //    ms.Close();
        //    sock.Send(byteData_StringSize);

        //    //전송(2) - 파일 정보를 메시지로 전송 (파일이 많으면 길이가 길어질 수 있으니 패킷 단위로 분할 전송 한다)
        //    //byteData_StringSize = new byte[packetSize];
        //    byteData_StringValue = new byte[byteDataLength];
        //    Array.Copy(rawByte, 0, byteData_StringValue, 0, rawByte.Length);
        //    for (int i = 0; i < byteDataLength; i += packetSize) //패킷 단위로 분할 하여 데이터를 전송 한다
        //    {
        //        byte[] devidedByte = new byte[packetSize];
        //        Array.Copy(byteData_StringValue, i, devidedByte, 0, packetSize);
        //        sock.Send(devidedByte);
        //    }

        //    //전송(3) - 파일 정보를 Byte형식으로 전송 한다
        //    byteData_FileData = new byte[byteDataLength_FileData];
        //    int byteWriteIndex = 0;
        //    foreach (FileItem item in fileItemList)
        //    {
        //        FileStream fs = File.OpenRead(item.fileFullName);
        //        fs.Read(byteData_FileData, byteWriteIndex, item.fileSize_Bytes);
        //        byteWriteIndex += item.fileSize_Bytes;
        //        fs.Close();
        //    }


        //    //Array.Copy(fileBytes, 0, byteData_FileData, 0, fileBytes.Length);
        //    for (int i = 0; i < byteDataLength_FileData; i += packetSize) //패킷 단위로 분할 하여 데이터를 전송 한다
        //    {
        //        byte[] devidedByte = new byte[packetSize];
        //        Array.Copy(byteData_FileData, i, devidedByte, 0, packetSize);
        //        sock.Send(devidedByte);
        //    }


        //    //소켓을 닫는다
        //    sock.Close();

        //    connected = false;

        //    return "완료";
        //}


        private static bool WaitConnectionEnds(int timeout_sec = 5)
        {
            int currentWaitCount = 0;
            while (connected)
            {
                Thread.Sleep(100);
                if (currentWaitCount++ > timeout_sec * 1000 / 100)
                {
                    return false;
                }
            }
            return true;
        }

    }


}