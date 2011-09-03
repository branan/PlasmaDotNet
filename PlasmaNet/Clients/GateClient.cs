﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Plasma {
    public class pnGateClient : pnUnbufferedClient {

        public pnGateClient() : base() {
            fConnHdr.fType = ENetProtocol.kConnTypeCliToGate;
        }

        protected override void IOnConnect() {
            plBufferedStream bs = new plBufferedStream(new NetworkStream(fSocket, false));
            fConnHdr.Write(bs);
            bs.WriteInt(20);
            pnHelpers.WriteUuid(bs, Guid.Empty);
            bs.Flush();

            // Encryption
            if (!base.INetCliConnect(bs, 4))
                throw new plNetException("Modified DH exchange failed");
            bs.Close();

            // Listen
            base.IOnConnect();
        }

        public void Ping(uint ms, byte[] payload, pnCallback cb) {
            pnCli2Gate_PingRequest req = new pnCli2Gate_PingRequest();
            req.fPayload = payload;
            req.fPingTimeMs = ms;
            req.fTransID = GetTransID();

            lock (fStream) {
                if (cb != null)
                    fCallbacks.Add(req.fTransID, cb);
                req.Send(fStream);
            }
        }

        protected override void OnReceive() {
            try {
                lock (fStream) {
                    pnGate2Cli msgID = (pnGate2Cli)fStream.ReadUShort();
                    switch (msgID) {
                        case pnGate2Cli.kGateKeeper2Cli_PingReply:
                            IPingReply();
                            break;
                    }
                }
            } catch (EndOfStreamException) {
                // Disconnected in a strange way
                return;
            } catch (SocketException) {
                // Connection Reset OR something weird happened
                return;
            } catch (ObjectDisposedException) {
                // The socket was closed.
            }
        }

        private void IPingReply() {
            pnGate2Cli_PingReply reply = new pnGate2Cli_PingReply();
            reply.Read(fStream);
            FireCallback(reply.fTransID, new object[] { reply.fPingTimeMs, reply.fPayload });
        }
    }
}
