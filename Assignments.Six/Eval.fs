﻿module Eval

    open StateMonad

    (* Code for testing *)

    let hello = [('H', 4); ('E', 1); ('L', 1); ('L', 1); ('O', 1); ]
    let state = mkState [("x", 5); ("y", 42)] hello ["_pos_"; "_result_"]
    let emptyState = mkState [] [] []
    
    let binop f a b = a >>= (fun x -> b >>= fun y -> ret (f x y))

    let add a b = binop (+) a b     
    let div a b = a >>= (fun x -> b >>= fun y -> match y with 
                                                    | v when v = 0 -> fail DivisionByZero
                                                    | v -> ret (x/v))      

    let isVowel c = 
        match System.Char.ToUpper c with 
        | 'A' | 'E' | 'I' | 'O' | 'U' -> true
        | _ -> false;;

    type aExp =
        | N of int
        | V of string
        | WL
        | PV of aExp
        | Add of aExp * aExp
        | Sub of aExp * aExp
        | Mul of aExp * aExp
        | Div of aExp * aExp
        | Mod of aExp * aExp
        | CharToInt of cExp

    and cExp =
       | C  of char  (* Character value *)
       | CV of aExp  (* Character lookup at word index *)
       | ToUpper of cExp
       | ToLower of cExp
       | IntToChar of aExp

    type bExp =             
       | TT                   (* true *)
       | FF                   (* false *)

       | AEq of aExp * aExp   (* numeric equality *)
       | ALt of aExp * aExp   (* numeric less than *)

       | Not of bExp          (* boolean not *)
       | Conj of bExp * bExp  (* boolean conjunction *)

       | IsVowel of cExp      (* check for vowel *)
       | IsLetter of cExp     (* check for letter *)
       | IsDigit of cExp      (* check for digit *)

    let (.+.) a b = Add (a, b)
    let (.-.) a b = Sub (a, b)
    let (.*.) a b = Mul (a, b)
    let (./.) a b = Div (a, b)
    let (.%.) a b = Mod (a, b)

    let (~~) b = Not b
    let (.&&.) b1 b2 = Conj (b1, b2)
    let (.||.) b1 b2 = ~~(~~b1 .&&. ~~b2)       (* boolean disjunction *)
    let (.->.) b1 b2 = (~~b1) .||. b2           (* boolean implication *) 
       
    let (.=.) a b = AEq (a, b)   
    let (.<.) a b = ALt (a, b)   
    let (.<>.) a b = ~~(a .=. b)
    let (.<=.) a b = a .<. b .||. ~~(a .<>. b)
    let (.>=.) a b = ~~(a .<. b)                (* numeric greater than or equal to *)
    let (.>.) a b = ~~(a .=. b) .&&. (a .>=. b) (* numeric greater than *)    
    
    let rec arithEval a : SM<int> = 
        match a with
        | N n -> ret n
        | V str -> lookup str 
        | WL -> wordLength
        | PV a -> arithEval a >>= pointValue 
        | Add (a, b) -> add (arithEval a) (arithEval a) 
        | Sub (a, b) -> binop (-) (arithEval a) (arithEval a)
        | Mul (a, b) -> binop (*) (arithEval a) (arithEval a)
        | Div (a, b) -> div (arithEval a) (arithEval a)
        | Mod (a, b) -> arithEval a >>= (fun x -> arithEval b >>= fun y -> match y with 
                                                                            | v when v = 0 -> fail DivisionByZero
                                                                            | v -> ret (x % v))
        | CharToInt c -> c |> fun c -> 
                            let rec aux char =
                                match char with 
                                | C c -> ret c
                                | CV a -> arithEval a >>= characterValue
                                | ToUpper c ->  aux c >>= fun a -> ret (System.Char.ToUpper a)
                                | ToLower c -> aux c >>= fun a -> ret (System.Char.ToLower a) 
                                | IntToChar a -> arithEval a >>= fun b -> ret (System.Convert.ToChar b)
                            
                            aux c >>= fun ch -> ret (System.Convert.ToInt32 ch)

    let rec charEval c : SM<char> = 
        match c with 
        | C c -> ret c
        | CV a -> arithEval a >>= characterValue
        | ToUpper c ->  charEval c >>= fun a -> ret (System.Char.ToUpper a)
        | ToLower c -> charEval c >>= fun a -> ret (System.Char.ToLower a)
        | IntToChar a -> arithEval a >>= fun b -> ret (System.Convert.ToChar b)      

    let rec boolEval b : SM<bool> = 
        match b with 
        | TT -> ret true
        | FF -> ret false

        | AEq (a, b) -> binop (=) (arithEval a) (arithEval b)
        | ALt (a, b) -> binop (<) (arithEval a) (arithEval b)

        | Not b -> boolEval b >>= fun e -> ret (not(e))
        | Conj (a, b) -> binop (&&) (boolEval a) (boolEval b)

        | IsVowel c -> charEval c >>= fun a -> ret (isVowel a)
        | IsLetter c -> charEval c >>= fun a -> ret (System.Char.IsLetter a)
        | IsDigit c -> charEval c >>= fun a -> ret (System.Char.IsDigit a)


    type stm =                (* statements *)
    | Declare of string       (* variable declaration *)
    | Ass of string * aExp    (* variable assignment *)
    | Skip                    (* nop *)
    | Seq of stm * stm        (* sequential composition *)
    | ITE of bExp * stm * stm (* if-then-else statement *)
    | While of bExp * stm     (* while statement *)

    let rec stmntEval stmnt : SM<unit> =
        match stmnt with 
        | Declare str -> declare str
        | Ass (str, exp) -> declare str >>>= arithEval exp >>= (fun vl -> update str vl)
        | Skip -> ret ()
        | Seq (s1, s2) -> stmntEval s1 >>>= stmntEval s2
        | ITE (exp, s1, s2) -> boolEval exp >>= fun b -> if b then stmntEval s1 else stmntEval s2 
        | While (exp, stm) -> boolEval exp >>= fun b -> if b then stmntEval stm >>>= stmntEval stmnt else ret ()

(* Part 3 (Optional) *)

    type StateBuilder() =

        member this.Bind(f, x)    = f >>= x
        member this.Return(x)     = ret x
        member this.ReturnFrom(x) = x
        member this.Delay(f)      = f ()
        member this.Combine(a, b) = a >>= (fun _ -> b)
        
    let prog = new StateBuilder()

    let arithEval2 a =
        match a with
        | N n -> ret n
        | V str -> lookup str 
        | WL -> wordLength
        | PV a -> arithEval a >>= pointValue 
        | Add (a, b) -> add (arithEval a) (arithEval a) 
        | Sub (a, b) -> binop (-) (arithEval a) (arithEval a)
        | Mul (a, b) -> binop (*) (arithEval a) (arithEval a)
        | Div (a, b) -> div (arithEval a) (arithEval a)
        | Mod (a, b) -> arithEval a >>= (fun x -> arithEval b >>= fun y -> match y with 
                                                                            | v when v = 0 -> fail DivisionByZero
                                                                            | v -> ret (x % v))
        | CharToInt c -> c |> fun c -> 
                            let rec aux char =
                                match char with 
                                | C c -> ret c
                                | CV a -> arithEval a >>= characterValue
                                | ToUpper c ->  aux c >>= fun a -> ret (System.Char.ToUpper a)
                                | ToLower c -> aux c >>= fun a -> ret (System.Char.ToLower a) 
                                | IntToChar a -> arithEval a >>= fun b -> ret (System.Convert.ToChar b)
                            
                            aux c >>= fun ch -> ret (System.Convert.ToInt32 ch)
                            
    let charEval2 c = failwith "Not implemented"
    let rec boolEval2 b = failwith "Not implemented"

    let stmntEval2 stm = failwith "Not implemented"

(* Part 4 (Optional) *) 

    type word = (char * int) list
    type squareFun = word -> int -> int -> Result<int, Error>

    let stmntToSquareFun stm = failwith "Not implemented"


    type coord = int * int

    type boardFun = coord -> Result<squareFun option, Error> 

    let stmntToBoardFun stm m = failwith "Not implemented"

    type board = {
        center        : coord
        defaultSquare : squareFun
        squares       : boardFun
    }

    let mkBoard c defaultSq boardStmnt ids = failwith "Not implemented"
    