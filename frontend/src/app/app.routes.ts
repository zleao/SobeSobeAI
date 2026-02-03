import { Routes } from '@angular/router';
import { Login } from './pages/login/login';
import { Register } from './pages/register/register';
import { Lobby } from './pages/lobby/lobby';
import { GameRoom } from './pages/game-room/game-room';
import { GameBoard } from './pages/game-board/game-board';
import { inject } from '@angular/core';
import { Auth } from './services/auth';
import { Router } from '@angular/router';

const authGuard = () => {
  const authService = inject(Auth);
  const router = inject(Router);
  
  if (authService.isAuthenticated()) {
    return true;
  }
  
  router.navigate(['/login']);
  return false;
};

const guestGuard = () => {
  const authService = inject(Auth);
  const router = inject(Router);
  
  if (!authService.isAuthenticated()) {
    return true;
  }
  
  router.navigate(['/lobby']);
  return false;
};

export const routes: Routes = [
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  { path: 'login', component: Login, canActivate: [guestGuard] },
  { path: 'register', component: Register, canActivate: [guestGuard] },
  { path: 'lobby', component: Lobby, canActivate: [authGuard] },
  { path: 'game-room/:id', component: GameRoom, canActivate: [authGuard] },
  { path: 'game-board/:id', component: GameBoard, canActivate: [authGuard] },
  { path: '**', redirectTo: '/login' }
];
