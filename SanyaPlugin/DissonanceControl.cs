using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Dissonance;
using Dissonance.Audio.Capture;
using Dissonance.Audio.Codecs;
using Dissonance.Networking;
using Dissonance.Integrations.MirrorIgnorance;
using NAudio.Wave;
using Mirror;
using MEC;
using Dissonance.Config;
using Dissonance.Networking.Client;
using System.Linq;

namespace SanyaPlugin.DissonanceControl
{
	internal static class DissonanceCommsControl
	{
		public static bool isReady { get; private set; } = false;
		public static DissonanceComms dissonanceComms = null;
		public static MirrorIgnoranceCommsNetwork mirrorComms = null;
		public static MirrorIgnoranceClient mirrorClient = null;
		public static ClientInfo<MirrorConn> mirrorClientInfo = null;
		public static StreamCapture streamCapture = null;

		public static void Init()
		{
			try
			{
				dissonanceComms = UnityEngine.Object.FindObjectOfType<DissonanceComms>();
				mirrorComms = UnityEngine.Object.FindObjectOfType<MirrorIgnoranceCommsNetwork>();

				mirrorComms.StartClient(Unit.None);
				mirrorComms.Mode = NetworkMode.Host;
				mirrorClient = mirrorComms.Client;
				mirrorClientInfo = mirrorComms.Server._clients.GetOrCreateClientInfo(3781, "SanyaPlugin_Host",
					new CodecSettings(Codec.Opus, 960, 48000),
					new MirrorConn(NetworkServer.localConnection)
				);

				streamCapture = dissonanceComms.gameObject.AddComponent<StreamCapture>();
				dissonanceComms._capture.Start(mirrorComms, streamCapture);
				dissonanceComms._capture.MicrophoneName = "StreamingMic";
				dissonanceComms.IsMuted = false;

				foreach(var i in dissonanceComms.RoomChannels._openChannelsBySubId.ToArray())
					dissonanceComms.RoomChannels.Close(i.Value);
				mirrorComms.Server._clients.LeaveRoom(TriggerType.Intercom.ToString(), mirrorClientInfo);
				mirrorComms.Server._clients.JoinRoom(TriggerType.Intercom.ToString(), mirrorClientInfo);
				dissonanceComms.RoomChannels.Open(TriggerType.Intercom.ToString(), false, ChannelPriority.None, SanyaPlugin.Instance.Config.DissonanceVolume);

				isReady = true;
			}
			catch(Exception e)
			{
				Exiled.API.Features.Log.Error($"[Init] {e}");
			}
		}

		public static void ChangeVolume(float volume)
		{
			if(dissonanceComms == null || mirrorComms == null) return;

			foreach(var i in dissonanceComms.RoomChannels._openChannelsBySubId.ToArray())
				dissonanceComms.RoomChannels.Close(i.Value);
			mirrorComms.Server._clients.LeaveRoom(TriggerType.Intercom.ToString(), mirrorClientInfo);
			mirrorComms.Server._clients.JoinRoom(TriggerType.Intercom.ToString(), mirrorClientInfo);
			dissonanceComms.RoomChannels.Open(TriggerType.Intercom.ToString(), false, ChannelPriority.None, volume);
		}

		public static void Dispose()
		{
			streamCapture.StopCapture();
			streamCapture = null;
			mirrorClientInfo = null;
			mirrorComms.StopClient();
			mirrorClient = null;
			mirrorComms = null;
			dissonanceComms = null;
			isReady = false;
		}
	}

	public class StreamCapture : MonoBehaviour, IMicrophoneCapture
	{
		public bool IsRecording => _stream != null && _stream.CanRead;
		public TimeSpan Latency { get; private set; }
		private readonly List<IMicrophoneSubscriber> _subscribers = new List<IMicrophoneSubscriber>();
		public void Subscribe(IMicrophoneSubscriber listener) => this._subscribers.Add(listener);
		public bool Unsubscribe(IMicrophoneSubscriber listener) => this._subscribers.Remove(listener);
		private readonly WaveFormat waveFormat = new WaveFormat(48000, 1);
		private string _name = string.Empty;
		private string _fullpath = string.Empty;
		private FileStream _stream = null;
		private float[] _frame = new float[960];
		private byte[] _frameByte = new byte[960 * 4];
		private float _elapsedTime;

		public WaveFormat StartCapture(string name)
		{
			_name = name;

			try
			{
				if(name == "StreamingMic")
				{
					Exiled.API.Features.Log.Debug($"[StreamCapture] Init");
				}
				else
				{
					Exiled.API.Features.Log.Info($"[StreamCapture] Loading:{name}");
					_fullpath = Path.Combine(SanyaPlugin.Instance.Config.DissonanceDataDirectory, name);
					_stream = File.OpenRead(_fullpath);

					if(_stream == null || !_stream.CanRead)
						Exiled.API.Features.Log.Error($"[StreamCapture] Failed:{name} IsStreamNull:{_stream == null} CanRead:{_stream?.CanRead}");
				}
			}
			catch(FileNotFoundException e)
			{
				Exiled.API.Features.Log.Error($"[StartCapture] {e.Message}");
				_stream = null;
			}
			catch(Exception e)
			{
				Exiled.API.Features.Log.Error($"[StartCapture] {e}");
				_stream = null;
			}

			Latency = TimeSpan.FromMilliseconds(0);
			return waveFormat;
		}

		public void StopCapture()
		{
			Exiled.API.Features.Log.Info($"[StreamCapture] Stopped:{_name}");
			_stream?.Dispose();
			_stream?.Close();
			_stream = null;
		}

		public bool UpdateSubscribers()
		{
			if(_stream == null) return true;

			_elapsedTime += Time.unscaledDeltaTime;

			while(_elapsedTime > 0.02f)
			{
				_elapsedTime -= 0.02f;

				var length = _stream.Read(_frameByte, 0, _frameByte.Length);

				if(length == 0)
				{
					StopCapture();
					return true;
				}

				Array.Clear(_frame, 0, _frame.Length);
				Buffer.BlockCopy(_frameByte, 0, _frame, 0, length);

				foreach(var subscriber in _subscribers)
					subscriber.ReceiveMicrophoneData(new ArraySegment<float>(_frame), waveFormat);

			}
			return false;
		}
	}
}
