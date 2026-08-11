import { describe, it, expect, vi } from 'vitest';
import { createDraft } from '../draftRetention';

describe('draftRetention — commit lifecycle', () => {
  it('commits on submit and records the last committed value', async () => {
    const commit = vi.fn().mockResolvedValue(true);
    const draft = createDraft<number>({ validate: () => null, commit });
    draft.start(250);
    expect(draft.isPending()).toBe(true);
    expect(draft.blocksNavigation()).toBe(true);
    expect(await draft.submit()).toBe('committed');
    expect(commit).toHaveBeenCalledWith(250);
    expect(draft.getState().lastCommitted).toBe(250);
    expect(draft.blocksNavigation()).toBe(false);
  });

  it('discard drops the pending draft', async () => {
    const draft = createDraft<number>({ validate: () => null, commit: vi.fn().mockResolvedValue(true) });
    draft.start(250);
    draft.discard();
    expect(draft.isPending()).toBe(false);
    expect(await draft.submit()).toBe('idle');
  });
});

describe('draftRetention — validation rejection', () => {
  it('rejects with a reason and reverts to the last committed value', async () => {
    const draft = createDraft<number>({ validate: (v) => (v > 60000 ? 'clamp' : null), commit: vi.fn().mockResolvedValue(true) });
    draft.start(75000);
    expect(await draft.submit()).toBe('rejected');
    expect(draft.getState().reason).toBe('clamp');
    expect(draft.blocksNavigation()).toBe(false); // rejected ≠ retained failed draft
  });

  it('acknowledgeRejection clears the reason', () => {
    const draft = createDraft<number>({ validate: () => 'bad', commit: vi.fn() });
    draft.start(1);
    void draft.submit().then(result => {
      expect(result).toBe('rejected');
      draft.acknowledgeRejection();
      expect(draft.getState().reason).toBeNull();
    });
  });
});

describe('draftRetention — commit failure', () => {
  it('retains the failed draft on screen and blocks navigation until discarded', async () => {
    const commit = vi.fn().mockResolvedValue(false);
    const draft = createDraft<number>({ validate: () => null, commit });
    draft.start(250);
    expect(await draft.submit()).toBe('failed');
    expect(draft.getState().value).toBe(250);
    expect(draft.blocksNavigation()).toBe(true);

    draft.discard();
    expect(draft.blocksNavigation()).toBe(false);
  });

  it('a later submit retries after the failure', async () => {
    const commit = vi.fn().mockResolvedValueOnce(false).mockResolvedValueOnce(true);
    const draft = createDraft<number>({ validate: () => null, commit });
    draft.start(250);
    expect(await draft.submit()).toBe('failed');
    expect(await draft.submit()).toBe('committed');
    expect(commit).toHaveBeenCalledTimes(2);
  });
});
