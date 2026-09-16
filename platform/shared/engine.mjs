// Pure clock: both renderers use the same action order, gait and reminder timing.
import {ACTIONS} from './pet-package.mjs';
const DURATION = { idle: 10, walk: 6.72, stretch: 3.2, sleep: 32 };
export class PetEngine {
  constructor(pet, random = Math.random) {
    this.random = random;
    this.elapsed = 0;
    this.direction = -1;
    this.x = 0;
    this.paused = false;
    this.manualSleep = false;
    this.action = 'idle';
    this.bag = [];
    this.reminderClock = 0;
    this.focusRemaining = null;
    this.setPet(pet);
    this.limit = 5;
  }
  setPet(pet) {
    this.pet = pet;
    this.available = Object.keys(pet.actions);
    this.bag = this.bag.filter(action => this.available.includes(action));
    if (!this.available.includes(this.action)) this.play('idle');
  }
  play(action, manual = true) {
    if (!this.pet.actions[action]) return false;
    this.action = action;
    this.elapsed = 0;
    this.limit = DURATION[action];
    this.manualSleep = manual && action === 'sleep';
    this.paused = false;
    return true;
  }
  cycle() {
    const choices = ACTIONS.filter(action => this.pet.actions[action]);
    if (choices.length < 2) return false;
    return this.play(choices[(choices.indexOf(this.action) + 1) % choices.length]);
  }
  wake() {
    this.manualSleep = false;
    this.play(this.pet.actions.stretch ? 'stretch' : 'idle', false);
  }
  next() {
    if (this.action !== 'idle') return 'idle';
    if (!this.bag.length) {
      this.bag = this.available.filter(action => action !== 'idle');
      for (let i = this.bag.length - 1; i > 0; i--) {
        const j = Math.floor(this.random() * (i + 1));
        [this.bag[i], this.bag[j]] = [this.bag[j], this.bag[i]];
      }
    }
    return this.bag.shift() || 'idle';
  }
  tick(deltaSeconds, width = 0, holdMotion = false) {
    const wall = Number.isFinite(deltaSeconds) ? Math.max(0, deltaSeconds) : 0;
    const dt = Math.min(wall, 0.1);
    const events = [];
    let dx = 0;
    if (!this.paused) {
      if (!holdMotion) {
        this.elapsed += dt;
        if (!this.manualSleep && this.elapsed >= this.limit) {
          const next = this.pet.settings.autoPlay ? this.next() : 'idle';
          this.play(next, false);
          if (next === 'idle') this.limit = (this.pet.settings.activity === 'lively' ? 5 : 10) + this.random() * 4;
        }
        if (this.action === 'walk') {
          const ramp = Math.min(1, this.elapsed / 0.56, Math.max(0, (this.limit - this.elapsed) / 0.56));
          dx = dt * 52 * ramp * this.direction;
          this.x += dx;
          if (width > 0) {
            const half = width / 2;
            if (Math.abs(this.x) > half) {
              this.x = Math.max(-half, Math.min(half, this.x));
              this.direction *= -1;
            }
          }
        }
      }
      if (this.pet.settings.reminder.enabled) {
        this.reminderClock += wall;
        if (this.reminderClock >= this.pet.settings.reminder.minutes * 60) {
          this.reminderClock = 0;
          events.push(this.pet.settings.reminder.message);
        }
      } else this.reminderClock = 0;
      if (this.focusRemaining !== null) {
        this.focusRemaining = Math.max(0, this.focusRemaining - wall);
        if (this.focusRemaining === 0) {
          this.focusRemaining = null;
          events.push('这段专注完成了，休息一下吧。');
        }
      }
    }
    const clip = this.pet.actions[this.action];
    const frame = Math.floor(this.elapsed * clip.fps) % clip.frames.length;
    return { action: this.action, assetId: clip.frames[frame], direction: this.direction, x: this.x, dx,
      breath: this.paused ? 0 : Math.sin(this.elapsed * (this.action === 'sleep' ? 1.5 : 2.2)) * 0.008,
      events, focusRemaining: this.focusRemaining, paused: this.paused };
  }
}
export function clampPosition(x, y, width, height, workArea) {
  return {
    x: Math.max(workArea.x, Math.min(workArea.x + Math.max(0, workArea.width - width), Math.round(x))),
    y: Math.max(workArea.y, Math.min(workArea.y + Math.max(0, workArea.height - height), Math.round(y))),
  };
}
