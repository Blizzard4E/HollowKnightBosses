using Terraria;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace HollowKnightBosses.Content.NPCs.Bosses.FalseKnight
{
	public class FalseKnight : ModNPC
	{
		// State machine states
		public enum BossState
		{
			Idle,
			Chase,
			AttackAnticipate,
			Attack,
			AttackRecovery
		}

		private BossState currentState = BossState.Idle; // Start idle
		private int stateTimer = 0;
		private int stuckTimer = 0;
		private Vector2 lastPosition = Vector2.Zero;
		private Vector2 lastStuckJumpPosition = Vector2.Zero;
		// Sprite sheet is 720x13680 with frames stacked vertically, each frame is 720x720
		private const int FRAME_WIDTH = 720;
		private const int FRAME_HEIGHT = 720;
		// For pit jump stuck detection
		private int consecutivePitJumps = 0;

		public override void SetStaticDefaults()
		{
		Main.npcFrameCount[Type] = 19; // Total frames: 5 (idle) + 6 (anticipate) + 4 (attack) + 4 (recovery)
	}

	public override void SetDefaults()
		{
			NPC.width = 50;  // Collision box width
			NPC.height = 50; // Collision box height (reduced to prevent sinking)
			NPC.damage = 15;
			NPC.defense = 5;
			NPC.lifeMax = 100;
			NPC.knockBackResist = 0.5f;
			NPC.aiStyle = -1; // Custom AI
			NPC.noGravity = false;
			NPC.noTileCollide = false;
		NPC.chaseable = false; // Won't be chased by other NPCs
	}

	public override void AI()
	{
		// Update state based on current state machine
		UpdateState();

		// Handle animations
		UpdateAnimation();

		// Bump other NPCs out of the way
		for (int i = 0; i < Main.maxNPCs; i++)
		{
			NPC otherNPC = Main.npc[i];
			if (otherNPC.active && i != NPC.whoAmI && !otherNPC.dontTakeDamage)
			{
				float distance = Vector2.Distance(NPC.Center, otherNPC.Center);
				if (distance < NPC.width + otherNPC.width)
				{
					// Push the other NPC away
					Vector2 pushDirection = Vector2.Normalize(otherNPC.Center - NPC.Center);
					otherNPC.velocity += pushDirection * 2f;
				}
			}
		}}

	/// <summary>
	/// Main state machine logic
	/// </summary>
	private void UpdateState()
	{
		// Always find the closest player
		NPC.TargetClosest(true);
			
			if (NPC.target < 0 || NPC.target >= Main.maxPlayers || Main.player[NPC.target] == null)
			{
				return;
			}

			Player target = Main.player[NPC.target];
			
			if (!target.active)
			{
				return;
			}

			switch (currentState)
			{
				case BossState.Idle:
					HandleIdleState(target);
					break;

				case BossState.Chase:
					HandleChaseState(target);
					break;

				case BossState.AttackAnticipate:
					HandleAttackAnticipateState(target);
					break;

				case BossState.Attack:
					HandleAttackState(target);
					break;

				case BossState.AttackRecovery:
					HandleAttackRecoveryState(target);
					break;
			}

			// Keep NPC in world bounds
			if (NPC.position.Y > Main.maxTilesY * 16)
			{
				NPC.active = false;
			}
		}

		/// <summary>
		/// Idle state: Stand still and wait for player to get close
		/// Idle frames: 0-4 (5 frames total)
		/// Duration per frame: 12 ticks (60 fps / 5 = 12 ticks per frame)
		/// </summary>
		private void HandleIdleState(Player target)
		{
			// Stop movement
			NPC.velocity.X = 0;

			stateTimer++;

			// Transition to Chase if player is within detection range
			float distanceToTarget = Vector2.Distance(NPC.Center, target.Center);
			if (distanceToTarget < 500)
			{
				ChangeState(BossState.Chase);
			}
		}

		/// <summary>
		/// Chase state: Move towards the player
		/// Chase frames: 0-4 (reusing idle animation)
		/// Duration per frame: 12 ticks
		/// </summary>
		private void HandleChaseState(Player target)
		{
			float distanceToTarget = Vector2.Distance(NPC.Center, target.Center);

			// Always move towards player
			Vector2 directionToTarget = Vector2.Normalize(target.Center - NPC.Center);
			NPC.velocity.X = directionToTarget.X * 4f;

			// --- Stuck Detection --- 
			if (NPC.velocity.Y == 0)
			{
				if ((NPC.Center - lastPosition).LengthSquared() < 0.5f)
				{
					stuckTimer++;
					// Only jump if stuck for 30 ticks and not at the same spot as last jump
					if (stuckTimer > 30 && (NPC.Center - lastStuckJumpPosition).LengthSquared() > 16f)
					{
						NPC.velocity.Y = -30f;
						stuckTimer = 0;
						lastStuckJumpPosition = NPC.Center;
					}
				}
				else
				{
					stuckTimer = 0;
				}
			}
			else
			{
				stuckTimer = 0;
			}
			lastPosition = NPC.Center;
			 

			// -- jump over small steps (optional) --
			Vector2 feet = NPC.Bottom + new Vector2(0, 2f);
			int feetTileX = (int)(feet.X / 16f);
			int feetTileY = (int)(feet.Y / 16f);
			bool onSolidGround = WorldGen.SolidTile(feetTileX, feetTileY);
			// --- Consecutive pit jump logic ---
			if (!onSolidGround && NPC.velocity.Y == 0)
			{
				consecutivePitJumps++;
				if (consecutivePitJumps >= 15)
				{
					NPC.velocity.Y = -30f; // Big jump out of deep pit
					consecutivePitJumps = 0;
				}
				else
				{
					NPC.velocity.Y = -10f; // Normal pit jump
				}
			}
			else if (onSolidGround)
			{
				consecutivePitJumps = 0;
			}

		// Apply gravity for vertical movement
		if (NPC.velocity.Y < 16f)
		{
			NPC.velocity.Y += 0.8f;
		}

		stateTimer++;

		// Trigger attack if player is within attack range
		if (distanceToTarget < 120)
		{
			ChangeState(BossState.AttackAnticipate);
		}
	}

	/// <summary>
	/// Attack Anticipate state: Wind up for attack
	/// Attack Anticipate frames: 5-10 (6 frames total)
	/// Duration: 1 second (60 ticks / 6 frames = 10 ticks per frame)
	/// </summary>
	private void HandleAttackAnticipateState(Player target)
		{
			// No horizontal movement during attack
			NPC.velocity.X = 0;

			stateTimer++;

			// After 60 ticks (1 second), transition to Attack
			if (stateTimer >= 60)
			{
				ChangeState(BossState.Attack);
			}
		}

		/// <summary>
		/// Attack state: Fast attack animation
		/// Attack frames: 0-3 (4 frames total)
		/// Duration: ~0.3 seconds (18 ticks / 4 frames = 4.5 ticks per frame, making it snappy)
		/// </summary>
		private void HandleAttackState(Player target)
		{
			// No horizontal movement during attack
			NPC.velocity.X = 0;

			stateTimer++;

			// After ~18 ticks, transition to Recovery
			if (stateTimer >= 18)
			{
				ChangeState(BossState.AttackRecovery);
			}
		}

		/// <summary>
		/// Attack Recovery state: Cool down after attack
		/// Attack Recovery frames: 15-18 (4 frames total)
		/// Duration: ~0.5 seconds (48 ticks / 4 frames = 12 ticks per frame)
		/// </summary>
		private void HandleAttackRecoveryState(Player target)
		{
			// No horizontal movement during recovery
			NPC.velocity.X = 0;

			stateTimer++;

			// Transition back to Chase after animation completes
			if (stateTimer > 48)
			{
				ChangeState(BossState.Chase);
			}
		}

		/// <summary>
		/// Change state and reset timer
		/// </summary>
		private void ChangeState(BossState newState)
		{
			currentState = newState;
			stateTimer = 0;
		}

		/// <summary>
		/// Handle sprite animation based on current state
		/// Vertical sprite sheet layout (stacked frames, 720x13680 = 19 frames max):
		/// Frames 0-4: Idle (5 frames)
		/// Frames 0-4: Chase (reuses Idle)
		/// Frames 5-10: Attack Anticipate (6 frames)
		/// Frames 11-14: Attack (4 frames)
		/// Frames 15-18: Attack Recovery (4 frames)
		/// </summary>
		private void UpdateAnimation()
		{
			switch (currentState)
			{
				case BossState.Idle:
					AnimateState(0, 5, 12); // Frames 0-4, 5 frames, 12 ticks per frame
					break;

				case BossState.Chase:
					AnimateState(0, 5, 12); // Frames 0-4 (reuse idle), 5 frames, 12 ticks per frame
					break;

				case BossState.AttackAnticipate:
					AnimateState(5, 6, 10); // Frames 5-10, 6 frames, ~10 ticks per frame
					break;

				case BossState.Attack:
					AnimateState(11, 4, 4); // Frames 11-14, 4 frames, ~4.5 ticks per frame (fast)
					break;

				case BossState.AttackRecovery:
					AnimateState(15, 4, 12); // Frames 15-18, 4 frames, 12 ticks per frame
					break;
			}

			// Flip sprite based on direction
			if (NPC.velocity.X < 0)
			{
				NPC.spriteDirection = -1;
			}
			else if (NPC.velocity.X > 0)
			{
				NPC.spriteDirection = 1;
			}
		}

		/// <summary>
		/// Animate a specific state
		/// Frames are stacked vertically in the sprite sheet (720x13680 = 19 frames max)
		/// </summary>
		/// <param name="frameStart">Index of first frame in this state</param>
		/// <param name="frameCount">Number of frames in this state</param>
		/// <param name="ticksPerFrame">How many ticks to display each frame</param>
		private void AnimateState(int frameStart, int frameCount, int ticksPerFrame)
		{
			int frameInState = stateTimer / ticksPerFrame;
			
			// Attack animations play once and hold on the final frame
			if (currentState == BossState.AttackAnticipate || currentState == BossState.Attack || currentState == BossState.AttackRecovery)
			{
				frameInState = Math.Min(frameInState, frameCount - 1);
			}
			else
			{
				// Idle and Chase loop continuously
				frameInState = frameInState % frameCount;
			}
			
			int totalFrame = frameStart + frameInState;
			NPC.frame.Y = totalFrame * FRAME_HEIGHT;
		}

		/// <summary>
		/// Custom draw method to properly display large sprite frames
		/// </summary>
		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			Texture2D texture = ModContent.Request<Texture2D>("HollowKnightBosses/Content/NPCs/Bosses/FalseKnight/FalseKnight").Value;
			
			float scale = 0.5f; // Adjust this to make the sprite bigger or smaller
			
			// Draw the sprite with feet at ground level
			Vector2 bottomPos = new Vector2(NPC.Center.X, NPC.position.Y + NPC.height);
			Vector2 drawPosition = bottomPos - screenPos;
			
			Rectangle frameRect = new Rectangle(0, NPC.frame.Y, FRAME_WIDTH, FRAME_HEIGHT);
			
			// Origin at bottom-center of sprite so it sits on the ground
			Vector2 origin = new Vector2(FRAME_WIDTH / 2f, FRAME_HEIGHT);
			
			spriteBatch.Draw(
				texture,
				drawPosition,
				frameRect,
				drawColor,
				0f,
				origin,
				scale,
				NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
				0f
			);
			
			return false; // Don't draw the default sprite
		}
	}
}
