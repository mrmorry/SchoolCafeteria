import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { Button } from './Button';

describe('Button', () => {
  it('renders its label and responds to click', () => {
    const onClick = vi.fn();
    render(<Button onClick={onClick}>Cobrar</Button>);
    const button = screen.getByRole('button', { name: 'Cobrar' });
    fireEvent.click(button);
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('is disabled and non-interactive when disabled is set', () => {
    const onClick = vi.fn();
    render(<Button onClick={onClick} disabled>Cobrar</Button>);
    fireEvent.click(screen.getByRole('button', { name: 'Cobrar' }));
    expect(onClick).not.toHaveBeenCalled();
  });
});
