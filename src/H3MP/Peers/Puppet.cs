using System;
using BepInEx.Logging;
using H3MP.Messages;
using H3MP.Models;
using H3MP.Utils;
using UnityEngine;

namespace H3MP
{
	public class Puppet : IRenderUpdatable, IDisposable
	{
		// Should slowly shrink to provide better reliability.
		private const double INTERP_DELAY_EMA_ALPHA = 1d / 10;

		private readonly ManualLogSource _log;
		private readonly double _minInterpDelay;

		private readonly GameObject _root;
		private readonly GameObject _head;
		private readonly GameObject _handLeft;
		private readonly GameObject _handRight;

		private readonly Func<ServerTime> _timeGetter;
		private readonly ExponentialMovingAverage _interpDelay;
		private readonly Snapshots<PlayerTransformsMessage> _snapshots;

		private ServerTime Time => _timeGetter();

		private static GameObject CreateRoot(ClientPuppetConfig config)
		{
			var root = new GameObject("Puppet Root");
			GameObject.DontDestroyOnLoad(root);

			root.transform.localScale = config.RootScale.Value;

			return root;
		}

		private static GameObject CreateLimb(GameObject root, ClientPuppetLimbConfig config)
		{
			var hand = GameObject.CreatePrimitive(PrimitiveType.Cube);

			// Components
			var transform = hand.transform;
			var renderer = hand.GetComponent<Renderer>();
			var collider = hand.GetComponent<Collider>();

			// Parent before scale (don't parent after)
			transform.parent = root.transform;
			transform.localScale = config.Scale.Value;

			// No collision
			GameObject.Destroy(collider);
			
			// Set color
			var mat = new Material(renderer.material);
			mat.color = config.Color.Value;
			renderer.material = mat;

			return hand;
		}

		internal Puppet(ManualLogSource log, ClientPuppetConfig config, Func<ServerTime> timeGetter, double tickDeltaTime)
		{
			_log = log;
			// A tick step for the remote client transmitting, server tranceiving, and local client receiving, each. 
			// Sometimes the stars align and no tick delay is achieved, but not on average, so the minimum should be when all the stars are perfectly not aligned.
			// Any further tweaking should be just because of network delay.
			//
			// If input-based movement is achieved, this can be reduced to 2.
			_minInterpDelay = 3 * tickDeltaTime;

			// Unity objects
			_root = CreateRoot(config);
			_head = CreateLimb(_root, config.Head);
			_handLeft = CreateLimb(_root, config.HandLeft);
			_handRight = CreateLimb(_root, config.HandRight);

			// .NET objects
			_timeGetter = timeGetter;
			_interpDelay = new ExponentialMovingAverage(_minInterpDelay, INTERP_DELAY_EMA_ALPHA);
			var killer = new TimeSnapshotKiller<PlayerTransformsMessage>(() => Time.Now, 5);
			_snapshots = new Snapshots<PlayerTransformsMessage>(killer);
		}

		public void ProcessTransforms(Timestamped<PlayerTransformsMessage> message)
		{
			var time = Time;
			if (!(time is null))
			{
				var messageDelay = time.Now - message.Timestamp;
				var interpDelay = _interpDelay.Value;

				if (messageDelay > interpDelay)
				{
					_interpDelay.Reset(messageDelay);
					_log.LogDebug($"A puppet's interpolation delay jumped ({interpDelay * 1000:N0} ms -> {messageDelay * 1000:N0} ms)");
				}
				else
				{
					_interpDelay.Push(Math.Max(messageDelay, _minInterpDelay));
				}
			}

			_snapshots.Push(message.Timestamp, message.Content);
		}

		public void RenderUpdate()
		{
			var time = Time;
			if (time is null)
			{
				return;
			}

			var snapshot = _snapshots[time.Now - _interpDelay.Value];

			snapshot.Head.Apply(_head.transform);
			snapshot.HandLeft.Apply(_handLeft.transform);
			snapshot.HandRight.Apply(_handRight.transform);
		}

		public void Dispose()
		{
			GameObject.Destroy(_head);
		}
	}
}