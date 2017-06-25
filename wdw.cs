using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

/**
 *  TODO: check if either player can move to block opponent
 *        if oponnent has a single move left and can block take it!
 *  TODO: evaluate build to avoid blocking myslef
 *  TODO: improve build score calculation (-1,0,1,2) is too coarse
 **/
class Player
{
    static string[] DIR = new string[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
    static int[][] DIR_DELTA = new int[][] { 
        new int[] { 0,-1}, /*N*/ new int[] { 1,-1}, /* NE */
        new int[] { 1, 0}, /*E*/ new int[] { 1, 1}, /* SE */
        new int[] { 0, 1}, /*S*/ new int[] {-1, 1}, /* SW */
        new int[] {-1, 0}, /*W*/ new int[] {-1,-1}  /* NW */
    };
    static int DToI(String dir) {
        switch(dir) {
            case "N": return 0;
            case "NE": return 1;
            case "E": return 2;
            case "SE": return 3;
            case "S": return 4;
            case "SW": return 5;
            case "W": return 6;
            case "NW": return 7;            
        }
        return -1;
    }

    static int SIZE = 7; static int MSIZE = SIZE * SIZE;
    
    static int[] Map;
    
    struct Position {
        public Position (int x, int y) { X = x; Y = y; }
        public int X { get; private set; }
        public int Y { get; private set; }
        
        public static Position NULL = new Position(-1,-1);
    }
    
    class Action {
        public bool IsMove { get; set; }    // MOVE&BUILD or PUSH&BUILD
        public int Id { get; set; }
        public int MoveDir { get; set; }
        public int BuildDir { get; set; }
        public double Score { get; set; }
    }
    
    // compute the score of moving to (tx, ty), for player Id
    static double MoveScore(int id, Position[] mine, Position[] their, int tx, int ty) {
        var p = id < mine.Length ? mine[id] : mine[mine.Length - 1];
        
        // check if tx and ty are valid (ie. on the board)
        if (tx < 0 || tx >= SIZE || ty < 0 || ty >= SIZE) return -1;
        // check if txt and ty are not a hole
        int tm = ty * SIZE + tx;
        if (Map[tm] == -1 || Map[tm] > 3) return -1;
        // check that the gradient between position and target is in (-1, +1)
        int gradient = Map[tm] - Map[p.X + p.Y*SIZE];
        if (gradient > 1) return -1;        
        
        // check that the target cell is not occupied by another one of mine
        for (int i = 0; i < mine.Length; i++) {
            if(i != id && tx == mine[i].X && ty == mine[i].Y) return -1;
        }
        // check that the target cell is not occupied by another one of theirs 
        for (int i = 0; i < their.Length; i++) {
            if(tx == their[i].X && ty == their[i].Y) return -1;
        }
        
        
        double distToCenter = 1.0 - (Math.Abs(tx - (SIZE / 2.0)) + Math.Abs(ty - (SIZE / 2.0))) / SIZE;
        if (Map[tm] == 3) {
            return 3 + (distToCenter * 0.5);
        }
        
        if (gradient > 0) {
            return 2 + (distToCenter * 0.5);
        }
        if (gradient == 0) {
            return 1 + (distToCenter * 0.5);
        }
        return 0 + (distToCenter * 0.5);        
    }
    
    // compute the score of building at (tx, ty), for player Id
    static double BuildScore(int id, Position[] mine, Position[] their, int mx, int my, int tx, int ty) {
                
        // check if tx and ty are valid (ie. on the board)
        if (tx < 0 || tx >= SIZE || ty < 0 || ty >= SIZE) return -1;
        int tm = ty * SIZE + tx;
        if (Map[tm] == -1 || Map[tm] > 3) return -1;
        
        for (int i = 0; i < mine.Length; i++) {
            if(i != id && tx == mine[i].X && ty == mine[i].Y) return -1;
        }
        // check that the target cell is not occupied by another one of theirs 
        for (int i = 0; i < their.Length; i++) {
            if(tx == their[i].X && ty == their[i].Y) return -1;
        }
        
        int gradient = Map[tm] - Map[mx + my * SIZE]; // diff between 
        
        if (Map[tm] == 3) {
            return 0; // maybe 1 if the oponent is close by
        }
        
        if (gradient == 0) return 2;        
        if (gradient > 0) return 0;        
        return 1;
    }
    
    static String Print(Action a) {
        return String.Format("{0} {1} {2} {3}", (a.IsMove ? "MOVE&BUILD" : "PUSH&BUILD"), a.Id, DIR[a.MoveDir], DIR[a.BuildDir]);
    }
    static String Debug(Action a) {
        return String.Format("{0}: {1} {2} {3}", a.Id, (a.IsMove ? "MOVE" : "PUSH"), DIR[a.MoveDir], DIR[a.BuildDir]);
    }
    
    static List<Action> canEliminate(Position enemy, Position[] mine, Position oEnemy) {
        Position[] their = new Position[2];
        their[0] = enemy;
        their[1] = oEnemy;
        
        var pushActions = new List<Action>();
        for(int pi = 0; pi < mine.Length; pi++) {
            int dx = enemy.X - mine[pi].X;
            int dy = enemy.Y - mine[pi].Y;
            if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1) {
                int dirToEnemy = 0;
                if (dx == 0) {
                    if (dy > 0) {
                        dirToEnemy = 4; // enemy to the S: 4 
                    }else {
                        dirToEnemy = 0; // enemy to the N: 0
                    }
                }else if(dy == 0) {
                    if (dx > 0) {
                        dirToEnemy = 2; // enemy to the E: 2
                    }else {
                        dirToEnemy = 6; // enemy to the W: 6 
                    }
                }else if(dx > 0) {
                    if (dy > 0) {
                        dirToEnemy = 3; // enemy to SE: 3
                    }else {
                        dirToEnemy = 1; // enemy to NE: 1
                    }
                }else {
                    if (dy > 0) {
                        dirToEnemy = 5; // enemy to SW: 5
                    }else {
                        dirToEnemy = 7; // enemy to NW: 7
                    }
                }
                //Console.Error.WriteLine("{0},{1} {5} of {2}:{3},{4}", enemy.X, enemy.Y, pi, mine[pi].X, mine[pi].Y, DIR[dirToEnemy]);
                for (int pushI = -1; pushI < 2; pushI++) {
                    int pushDir = (8 + dirToEnemy + pushI) % 8;
                    int px = enemy.X + DIR_DELTA[pushDir][0];
                    int py = enemy.Y + DIR_DELTA[pushDir][1];
                    Console.Error.WriteLine("push {0} to {1},{2}", DIR[pushDir], px, py);
                    if (px < 0 || px >= SIZE || py < 0 || py >= SIZE) continue; // push off map..                        
                    int pMap = Map[px + py * SIZE];
                    if (pMap < 0 || pMap > 3) continue; // push is not valid cell..                                          
                    if ((px == mine[0].X && py == mine[0].Y) ||
                        (px == mine[1].X && py == mine[1].Y) ||
                        (px == oEnemy.X && py == oEnemy.Y)) {
                        continue;  // push invalid
                    }
                    int eMap = Map[enemy.X + enemy.Y * SIZE];
                    their[0] = new Position(px, py);
                    
                    if (pMap == eMap) {
                        if(pMap == 3) {
                            Console.Error.WriteLine(" horrible push");
                            continue;
                        }else {
                            
                            int moves = countMoves(their[0], mine);                            
                            if (moves == 0) {
                                double buildScore = BuildScore(pi, mine, their, mine[pi].X, mine[pi].Y, mine[pi].X + DIR_DELTA[dirToEnemy][0], mine[pi].Y + DIR_DELTA[dirToEnemy][1]);                                
                                Console.Error.WriteLine(" amazing push -> {0}", buildScore);                                 
                                pushActions.Add(new Action() {
                                    IsMove = false,
                                    Id = pi,
                                    MoveDir = dirToEnemy,
                                    BuildDir = pushDir,
                                    Score = 10 + buildScore
                                });
                            }else {
                                // maybe allow push if only single move left
                                Console.Error.WriteLine(" meh push");  
                            }
                        }
                    }else if (pMap < eMap) {
                        if (pMap < eMap-1) {
                            int moves = countMoves(their[0], mine);
                            if (moves == 0) {
                                double buildScore = BuildScore(pi, mine, their, mine[pi].X, mine[pi].Y, mine[pi].X + DIR_DELTA[dirToEnemy][0], mine[pi].Y + DIR_DELTA[dirToEnemy][1]);                                
                                Console.Error.WriteLine(" amazing push -> {0}", buildScore); 
                                pushActions.Add(new Action() {
                                    IsMove = false,
                                    Id = pi,
                                    MoveDir = dirToEnemy,
                                    BuildDir = pushDir,
                                    Score = 10 + buildScore
                                });
                            }else {
                                double buildScore = BuildScore(pi, mine, their, mine[pi].X, mine[pi].Y, mine[pi].X + DIR_DELTA[dirToEnemy][0], mine[pi].Y + DIR_DELTA[dirToEnemy][1]);                                
                                Console.Error.WriteLine(" great push -> {0}, moves {1}", buildScore, moves);   
                                pushActions.Add(new Action() {
                                    IsMove = false,
                                    Id = pi,
                                    MoveDir = dirToEnemy,
                                    BuildDir = pushDir,
                                    Score = (5 - moves/4) + buildScore * 0.5
                                });
                            }
                        }else {
                            int moves = countMoves(their[0], mine);
                            if (moves == 0) {
                                double buildScore = BuildScore(pi, mine, their, mine[pi].X, mine[pi].Y, mine[pi].X + DIR_DELTA[dirToEnemy][0], mine[pi].Y + DIR_DELTA[dirToEnemy][1]);                                
                                Console.Error.WriteLine(" amazing push -> {0}", buildScore);   
                                pushActions.Add(new Action() {
                                    IsMove = false,
                                    Id = pi,
                                    MoveDir = dirToEnemy,
                                    BuildDir = pushDir,
                                    Score = 10 + buildScore
                                });
                            }else {
                                double buildScore = BuildScore(pi, mine, their, mine[pi].X, mine[pi].Y, mine[pi].X + DIR_DELTA[dirToEnemy][0], mine[pi].Y + DIR_DELTA[dirToEnemy][1]);                                
                                Console.Error.WriteLine(" good push -> {0}, moves {1}", buildScore, moves);
                                pushActions.Add(new Action() {
                                    IsMove = false,
                                    Id = pi,
                                    MoveDir = dirToEnemy,
                                    BuildDir = pushDir,
                                    Score = (2.25 - moves/4.0) + buildScore
                                });
                            }
                        }
                    }else {
                        // maybe I could evaluate build to lock him out..
                        Console.Error.WriteLine(" bad push");
                        continue;
                    }
                }
            
            }
        }
        return pushActions;
    }
    
    static int countMoves(Position p, Position[] mine) {
        int moves = 0;
        for(int mi = 0; mi < 8; mi++) {
            int tx = p.X + DIR_DELTA[mi][0];
            int ty = p.Y + DIR_DELTA[mi][1];
            
            // check if tx and ty are valid (ie. on the board)
            if (tx < 0 || tx >= SIZE || ty < 0 || ty >= SIZE) continue;
            // check if txt and ty are not a hole
            int tm = ty * SIZE + tx;
            if (Map[tm] == -1 || Map[tm] > 3) continue;
            // check that the gradient between position and target is in (-1, +1)
            int gradient = Map[tm] - Map[p.X + p.Y*SIZE];
            if (gradient > 1) continue;        
            
            // check that the target cell is not occupied by another one of mine
            
            if(tx == mine[0].X && ty == mine[0].Y) continue;
            if(mine.Length > 0 && tx == mine[1].X && ty == mine[1].Y) continue;
            
            moves++;
        }
        return moves;
    }
    
    static void Main(string[] args)
    {
        string[] inputs;
        SIZE = int.Parse(Console.ReadLine()); MSIZE = SIZE * SIZE;      
        Map = new int[MSIZE];
        int unitsPerPlayer = int.Parse(Console.ReadLine());

        var mine = new Position[unitsPerPlayer];
        var their = new Position[unitsPerPlayer]; 

        int GAME_SCORE = 0;
        // game loop
        while (true)
        {
            for (int i = 0; i < SIZE; i++)
            {                
                string row = Console.ReadLine();
                for (int j = 0; j < SIZE; j++) {
                    if(row[j] == '.')
                        Map[i * SIZE + j] = -1;
                    else
                        Map[i * SIZE + j] = (int)(row[j] - '0');
                }
                //Console.Error.WriteLine(row);
            }
            for (int i = 0; i < unitsPerPlayer; i++)
            {
                inputs = Console.ReadLine().Split(' ');
                mine[i] = new Position(int.Parse(inputs[0]), int.Parse(inputs[1]));                
            }
            List<Action> pushActions = new List<Action>();
            for (int i = 0; i < unitsPerPlayer; i++)
            {
                inputs = Console.ReadLine().Split(' ');
                their[i] = new Position(int.Parse(inputs[0]), int.Parse(inputs[1]));                
                if (their[i].X != -1 && their[i].Y != -1) {
                    pushActions.AddRange(canEliminate(their[i], mine, (unitsPerPlayer > 1 ? their[(i+1) % 2] : Position.NULL)));                    
                }
                //Console.Error.WriteLine("Oponent: {0},{1}", their[i].X, their[i].Y);
            }
            if (pushActions.Count > 0) {
                Console.Error.WriteLine(" * {0} valid push actions", pushActions.Count);                
            }            
                    
            int legalActions = int.Parse(Console.ReadLine());                        
            for (int i = 0; i < legalActions; i++)
            {
                inputs = Console.ReadLine().Split(' ');
            }                                         
            
            Action nextAction = null;
            var validMoves = new List<Action>();
            for (int pi = 0; pi < unitsPerPlayer; pi++) {
                
                for (int mi = 0; mi < 8; mi++) {
                    double score = MoveScore(pi, mine, their, mine[pi].X + DIR_DELTA[mi][0], mine[pi].Y + DIR_DELTA[mi][1]);                                                
                    if(score >= 0) {
                        // Console.Error.WriteLine("Move: {0} --> score: {1}", DIR[mi], score);
                        // evaluate builds from move position
                        for (int bi = 0; bi < 8; bi++) {
                            double buildScore = BuildScore(pi, mine, their, mine[pi].X + DIR_DELTA[mi][0], mine[pi].Y + DIR_DELTA[mi][1], mine[pi].X + DIR_DELTA[mi][0] + DIR_DELTA[bi][0], mine[pi].Y + DIR_DELTA[mi][1] + DIR_DELTA[bi][1]);
                            // Console.Error.WriteLine("  Build: {0} --> score: {1}", DIR[bi], buildScore);
                            if(buildScore >= 0) {
                                validMoves.Add(new Action() {
                                        IsMove = true,
                                        Id = pi,
                                        MoveDir = mi,
                                        BuildDir = bi,
                                        Score = score + buildScore
                                    });
                            }
                        }                    
                    }
                }                                                
            }
            validMoves.AddRange(pushActions);
            
            foreach(var action in validMoves.OrderByDescending(a => a.Score)) {
                Console.Error.WriteLine("{0} -> {1}", Debug(action), action.Score);
            }
            
            nextAction = validMoves.OrderByDescending(va => va.Score).FirstOrDefault();                
            
            if (nextAction != null ) {         
                if (nextAction.IsMove) {
                    var unit = mine[nextAction.Id];
                    int ux = unit.X + DIR_DELTA[nextAction.MoveDir][0];
                    int uy = unit.Y + DIR_DELTA[nextAction.MoveDir][1];
                    if (Map[ux + uy * SIZE] == 3) {
                        GAME_SCORE++;
                    }
                }
                Console.WriteLine(Print(nextAction) + " " + GAME_SCORE);
            }else {
                Console.WriteLine("ACCEPT-DEFEAT " + GAME_SCORE);
            }
        }
    }
}